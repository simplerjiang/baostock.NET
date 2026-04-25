using System.Globalization;
using System.Text.Json;
using Baostock.NET.Http;
using Baostock.NET.Models;
using Baostock.NET.Util;

namespace Baostock.NET.Financials;

/// <summary>
/// 新浪财经现金流量表数据源
/// （<c>https://quotes.sina.cn/cn/api/openapi.php/CompanyFinanceService.getFinanceReport2022</c>，<c>source=llb</c>）。
/// </summary>
/// <remarks>
/// <para>仅实现 <see cref="GetCashFlowStatementAsync"/>；<see cref="GetBalanceSheetAsync"/> / <see cref="GetIncomeStatementAsync"/>
/// 抛 <see cref="NotSupportedException"/>，请分别使用 <see cref="SinaBalanceSheetSource"/> / <see cref="SinaIncomeStatementSource"/>。</para>
/// </remarks>
public sealed class SinaCashFlowSource : IFinancialStatementSource
{
    /// <summary>新浪财报接口 URL。</summary>
    public const string EndpointUrl =
        "https://quotes.sina.cn/cn/api/openapi.php/CompanyFinanceService.getFinanceReport2022";

    /// <summary>HTTP 请求超时；默认 15 秒。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>单次请求拉取的报告期数量（num 参数）；默认 10。</summary>
    public int DefaultFetchCount { get; init; } = 10;

    private readonly HttpDataClient _http;
    private readonly SourceHealthRegistry? _health;

    /// <summary>构造一个新浪现金流量表数据源。</summary>
    /// <param name="http">HTTP 客户端；为 <see langword="null"/> 时使用 <see cref="HttpDataClient.Default"/>。</param>
    /// <param name="health">健康注册表；为 <see langword="null"/> 时不做健康统计。</param>
    public SinaCashFlowSource(HttpDataClient? http = null, SourceHealthRegistry? health = null)
    {
        _http = http ?? HttpDataClient.Default;
        _health = health;
    }

    /// <inheritdoc />
    public string Name => "Sina";

    /// <inheritdoc />
    public int Priority => 1;

    /// <inheritdoc />
    public Task<IReadOnlyList<FullBalanceSheetRow>> GetBalanceSheetAsync(
        FinancialStatementRequest request,
        CancellationToken ct = default)
        => throw new NotSupportedException("Use SinaBalanceSheetSource for balance sheet.");

    /// <inheritdoc />
    public Task<IReadOnlyList<FullIncomeStatementRow>> GetIncomeStatementAsync(
        FinancialStatementRequest request,
        CancellationToken ct = default)
        => throw new NotSupportedException("Use SinaIncomeStatementSource for income statement.");

    /// <inheritdoc />
    public async Task<IReadOnlyList<FullCashFlowRow>> GetCashFlowStatementAsync(
        FinancialStatementRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var result = await FetchAsync(request, ct).ConfigureAwait(false);
            _health?.MarkSuccess(Name);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _health?.MarkFailure(Name, ex);
            if (ex is DataSourceException) throw;
            throw new DataSourceException(Name, $"Sina cash flow fetch failed: {ex.Message}", null, ex);
        }
    }

    private async Task<IReadOnlyList<FullCashFlowRow>> FetchAsync(
        FinancialStatementRequest request,
        CancellationToken ct)
    {
        var lowerCode = CodeFormatter.Parse(request.Code).LowercaseNoDot;
        var url = EndpointUrl
            + "?paperCode=" + lowerCode
            + "&source=llb"
            + "&type=0"
            + "&page=1"
            + "&num=" + DefaultFetchCount.ToString(CultureInfo.InvariantCulture);

        var json = await _http.GetStringAsync(
            url,
            BuildHeaders(),
            encoding: null,
            timeout: Timeout,
            ct: ct).ConfigureAwait(false);

        var rows = ParseResponse(json, request.Code);

        if (request.ReportDates is { Count: > 0 })
        {
            var set = new HashSet<DateOnly>(request.ReportDates);
            rows = rows.Where(r => set.Contains(r.ReportDate)).ToList();
        }

        // 与 EastMoney 数据源保持一致：空结果视为失败，避免 hedge runner 在此源预先完成时
        // 静默地返回 0 行并被上层误认为 ok＋ rowCount=0。
        if (rows.Count == 0)
        {
            throw new DataSourceException(Name, $"Sina getFinanceReport2022 (llb) returned no rows for '{request.Code}'.");
        }

        rows.Sort((a, b) => b.ReportDate.CompareTo(a.ReportDate));
        return rows;
    }

    /// <summary>构造新浪财报接口通用请求头。</summary>
    /// <returns>请求头字典。</returns>
    internal static Dictionary<string, string> BuildHeaders() => new()
    {
        ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        ["Referer"] = "https://finance.sina.com.cn/",
        ["Accept"] = "application/json",
    };

    /// <summary>容错解析 decimal：空 / 空白 / <c>"-"</c> / <c>"--"</c> / 非法 → <see langword="null"/>。</summary>
    /// <param name="s">待解析字符串。</param>
    /// <returns>解析成功返回 decimal；否则 <see langword="null"/>。</returns>
    internal static decimal? SafeParseDecimal(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        if (t == "-" || t == "--") return null;
        return decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    /// <summary>解析新浪 <c>getFinanceReport2022</c>（<c>source=llb</c>）响应为行列表（暴露用于离线单元测试）。</summary>
    /// <param name="jsonBody">响应 JSON 正文。</param>
    /// <param name="inputCode">请求时的原始代码（用于写回 <see cref="FullCashFlowRow.Code"/>）。</param>
    /// <returns>解析出的行列表（未排序）。</returns>
    /// <exception cref="DataSourceException">JSON 缺 <c>result.data.report_list</c>。</exception>
    internal static List<FullCashFlowRow> ParseResponse(string jsonBody, string inputCode)
    {
        using var doc = JsonDocument.Parse(jsonBody);
        var root = doc.RootElement;
        if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object
            || !result.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("report_list", out var reportList) || reportList.ValueKind != JsonValueKind.Object)
        {
            throw new DataSourceException("Sina", "Sina finance report response missing 'result.data.report_list'.");
        }

        var normalizedCode = CodeFormatter.TryParse(inputCode, out var sc) ? sc.EastMoneyForm : inputCode;
        var rows = new List<FullCashFlowRow>();

        foreach (var period in reportList.EnumerateObject())
        {
            if (period.Value.ValueKind != JsonValueKind.Object) continue;
            // 新浪实际返回 period key 为 "yyyyMMdd"（如 "20241231"），
            // 同时为兼容既有单元测试 fixture 里 "yyyy-MM-dd" 写法，两种格式都接受。
            if (!DateOnly.TryParseExact(period.Name, new[] { "yyyyMMdd", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var reportDate))
            {
                continue;
            }

            var raw = new Dictionary<string, string?>(StringComparer.Ordinal);
            string? reportTitle = null;
            if (period.Value.TryGetProperty("rType", out var rType) && rType.ValueKind == JsonValueKind.String)
            {
                reportTitle = rType.GetString();
                raw["rType"] = reportTitle;
            }
            if (period.Value.TryGetProperty("is_audit", out var audit) && audit.ValueKind == JsonValueKind.String)
            {
                raw["is_audit"] = audit.GetString();
            }
            if (period.Value.TryGetProperty("publish_date", out var pub) && pub.ValueKind == JsonValueKind.String)
            {
                raw["publish_date"] = pub.GetString();
            }
            if (period.Value.TryGetProperty("rCurrency", out var cur) && cur.ValueKind == JsonValueKind.String)
            {
                raw["rCurrency"] = cur.GetString();
            }

            decimal? salesServices = null, totalOperateInflow = null, totalOperateOutflow = null;
            // v1.4.0 起：MANANETR/NETCFOPER（经营活动现金流量净额, CFO）与
            // CASHNETR（现金及现金等价物净增加额）必须分开变量保存。v1.3.x 用 ??=
            // 把两者混入同一变量是 bug。
            decimal? mananetr = null;       // CFO：MANANETR 优先，备选 NETCFOPER
            decimal? cashIncrease = null;   // 现金净增加额：CASHNETR
            decimal? totalInvestInflow = null, totalInvestOutflow = null, netcashInvest = null;
            decimal? totalFinanceInflow = null, totalFinanceOutflow = null, netcashFinance = null;
            decimal? beginCce = null, endCce = null;

            if (period.Value.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dataArr.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    if (!item.TryGetProperty("item_field", out var fEl) || fEl.ValueKind != JsonValueKind.String) continue;
                    var field = fEl.GetString();
                    if (string.IsNullOrEmpty(field)) continue;

                    string? valueStr = null;
                    if (item.TryGetProperty("item_value", out var vEl))
                    {
                        valueStr = vEl.ValueKind switch
                        {
                            JsonValueKind.String => vEl.GetString(),
                            JsonValueKind.Null or JsonValueKind.Undefined => null,
                            _ => vEl.GetRawText(),
                        };
                    }
                    raw[field] = valueStr;

                    if (item.TryGetProperty("item_tongbi", out var tEl))
                    {
                        raw[field + "_TONGBI"] = tEl.ValueKind switch
                        {
                            JsonValueKind.String => tEl.GetString(),
                            JsonValueKind.Null or JsonValueKind.Undefined => null,
                            _ => tEl.GetRawText(),
                        };
                    }

                    var value = SafeParseDecimal(valueStr);
                    switch (field)
                    {
                        case "LABOREARN":
                        case "SALESSERVICE":
                            salesServices ??= value; break;
                        case "SUBTOTCASHINFL":
                        case "TOTALCASHINFL":
                            totalOperateInflow ??= value; break;
                        case "SUBTOTCASHOUTF":
                        case "TOTALCASHOUTF":
                            totalOperateOutflow ??= value; break;
                        case "CASHNETR":
                            cashIncrease ??= value; break;
                        case "MANANETR":
                            // MANANETR 优先级最高（直接覆盖 NETCFOPER 的备选值）
                            mananetr = value ?? mananetr; break;
                        case "NETCFOPER":
                            mananetr ??= value; break;
                        case "INFLINVE":
                        case "SUBTOTINVE":
                            totalInvestInflow ??= value; break;
                        case "OUTFINVE":
                        case "SUBTOTOUTFINVE":
                            totalInvestOutflow ??= value; break;
                        case "NETCASHINVE":
                        case "NETCFINVE":
                            netcashInvest ??= value; break;
                        case "INFLFINA":
                        case "SUBTOTFINA":
                            totalFinanceInflow ??= value; break;
                        case "OUTFFINA":
                        case "SUBTOTOUTFFINA":
                            totalFinanceOutflow ??= value; break;
                        case "NETCASHFINA":
                        case "NETCFFIN":
                            netcashFinance ??= value; break;
                        case "BEGPERIOCASH":
                        case "PEGPERBEGBAL":
                            beginCce ??= value; break;
                        case "ENDPERIOCASH":
                        case "PEGPERENDBAL":
                            endCce ??= value; break;
                        default: break;
                    }
                }
            }

            // 若接口未直接返回 CASHNETR，但三大类活动净额齐全，可派生现金净增加额。
            if (!cashIncrease.HasValue && mananetr.HasValue && netcashInvest.HasValue && netcashFinance.HasValue)
            {
                cashIncrease = mananetr.Value + netcashInvest.Value + netcashFinance.Value;
            }

#pragma warning disable CS0618 // NetcashOperate 标记 Obsolete，仍需为旧字段填值以保持兼容
            rows.Add(new FullCashFlowRow
            {
                Code = normalizedCode,
                ReportDate = reportDate,
                ReportTitle = reportTitle,
                SalesServices = salesServices,
                TotalOperateInflow = totalOperateInflow,
                TotalOperateOutflow = totalOperateOutflow,
                // v1.4.0 起 NetcashOperate 与 OperatingCashFlow 同值（统一为 CFO）。
                // 注意：v1.3.x Sina 路径 NetcashOperate 取的是 CASHNETR（净增加额），
                // 这是 BREAKING 修正——v1.3.x 的 Sina 行为本身错配了字段语义。
                NetcashOperate = mananetr,
                NetCashIncrease = cashIncrease,
                OperatingCashFlow = mananetr,
                TotalInvestInflow = totalInvestInflow,
                TotalInvestOutflow = totalInvestOutflow,
                NetcashInvest = netcashInvest,
                TotalFinanceInflow = totalFinanceInflow,
                TotalFinanceOutflow = totalFinanceOutflow,
                NetcashFinance = netcashFinance,
                BeginCce = beginCce,
                EndCce = endCce,
                RawFields = raw,
                Source = "Sina",
            });
#pragma warning restore CS0618
        }

        return rows;
    }
}
