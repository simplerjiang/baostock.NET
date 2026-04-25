using System.Text.Json;
using Baostock.NET.Http;
using Baostock.NET.Models;

namespace Baostock.NET.Financials;

/// <summary>
/// 东方财富现金流量表数据源（<c>https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/xjllbAjaxNew</c>）。
/// </summary>
/// <remarks>
/// <para>仅实现 <see cref="GetCashFlowStatementAsync"/>；另外两张报表请使用
/// <see cref="EastMoneyBalanceSheetSource"/> / <see cref="EastMoneyIncomeStatementSource"/>。</para>
/// <para>流程：嗅探 <see cref="CompanyType"/>（若未指定）→ 拉日期列表（若未指定，默认最近 10 期）→
/// 日期按每批 5 个切分调 <c>xjllbAjaxNew</c> → 合并 → 按 ReportDate 降序。</para>
/// </remarks>
public sealed class EastMoneyCashFlowSource : IFinancialStatementSource
{
    /// <summary>接口 base URL。</summary>
    public const string BaseUrl = "https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/";

    /// <summary>HTTP 请求超时；默认 15 秒。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>当调用方未传日期列表时默认拉取的最近期数。</summary>
    public int DefaultRecentCount { get; init; } = 10;

    private readonly HttpDataClient _http;
    private readonly EastMoneyCompanyTypeResolver _resolver;
    private readonly SourceHealthRegistry? _health;

    /// <summary>构造一个东财现金流量表数据源。</summary>
    /// <param name="http">HTTP 客户端；为 <see langword="null"/> 时使用 <see cref="HttpDataClient.Default"/>。</param>
    /// <param name="resolver">公司类型嗅探器；为 <see langword="null"/> 时使用 <see cref="EastMoneyCompanyTypeResolver.Default"/>。</param>
    /// <param name="health">健康注册表；为 <see langword="null"/> 时不做健康统计。</param>
    public EastMoneyCashFlowSource(
        HttpDataClient? http = null,
        EastMoneyCompanyTypeResolver? resolver = null,
        SourceHealthRegistry? health = null)
    {
        _http = http ?? HttpDataClient.Default;
        _resolver = resolver ?? EastMoneyCompanyTypeResolver.Default;
        _health = health;
    }

    /// <inheritdoc />
    public string Name => "EastMoney";

    /// <inheritdoc />
    public int Priority => 0;

    /// <inheritdoc />
    public Task<IReadOnlyList<FullBalanceSheetRow>> GetBalanceSheetAsync(
        FinancialStatementRequest request,
        CancellationToken ct = default)
        => throw new NotSupportedException("Use EastMoneyBalanceSheetSource for balance sheet.");

    /// <inheritdoc />
    public Task<IReadOnlyList<FullIncomeStatementRow>> GetIncomeStatementAsync(
        FinancialStatementRequest request,
        CancellationToken ct = default)
        => throw new NotSupportedException("Use EastMoneyIncomeStatementSource for income statement.");

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
            throw new DataSourceException(Name, $"EastMoney cash flow fetch failed: {ex.Message}", null, ex);
        }
    }

    private async Task<IReadOnlyList<FullCashFlowRow>> FetchAsync(
        FinancialStatementRequest request,
        CancellationToken ct)
    {
        var lowerCode = Baostock.NET.Util.CodeFormatter.Parse(request.Code).LowercaseNoDot;
        var companyType = request.CompanyType
            ?? await _resolver.ResolveAsync(request.Code, ct).ConfigureAwait(false);

        IReadOnlyList<DateOnly> dates;
        if (request.ReportDates is { Count: > 0 })
        {
            dates = request.ReportDates;
        }
        else
        {
            var dateUrl = BaseUrl + "xjllbDateAjaxNew"
                + "?companyType=" + (int)companyType
                + "&reportDateType=" + (int)request.DateType
                + "&code=" + lowerCode;
            var dateJson = await _http.GetStringAsync(
                dateUrl,
                EastMoneyCompanyTypeResolver.BuildHeaders(),
                encoding: null,
                timeout: Timeout,
                ct: ct).ConfigureAwait(false);
            var allDates = EastMoneyCompanyTypeResolver.ParseDateList(dateJson);
            if (allDates.Count == 0)
            {
                throw new DataSourceException(Name, $"EastMoney xjllbDateAjaxNew returned no dates for '{request.Code}'.");
            }
            dates = allDates.Take(DefaultRecentCount).ToList();
        }

        var all = new List<FullCashFlowRow>();
        foreach (var chunk in EastMoneyCompanyTypeResolver.ChunkDates(dates, 5))
        {
            var url = BaseUrl + "xjllbAjaxNew"
                + "?companyType=" + (int)companyType
                + "&reportDateType=" + (int)request.DateType
                + "&reportType=" + (int)request.ReportKind
                + "&dates=" + EastMoneyCompanyTypeResolver.FormatDatesParam(chunk)
                + "&code=" + lowerCode;
            var json = await _http.GetStringAsync(
                url,
                EastMoneyCompanyTypeResolver.BuildHeaders(),
                encoding: null,
                timeout: Timeout,
                ct: ct).ConfigureAwait(false);
            all.AddRange(ParseResponse(json, request.Code));
        }

        if (all.Count == 0)
        {
            throw new DataSourceException(Name, $"EastMoney xjllbAjaxNew returned no rows for '{request.Code}'.");
        }

        all.Sort((a, b) => b.ReportDate.CompareTo(a.ReportDate));
        return all;
    }

    /// <summary>解析 <c>xjllbAjaxNew</c> 响应为行列表（暴露用于离线单元测试）。</summary>
    /// <param name="jsonBody">响应 JSON 正文。</param>
    /// <param name="inputCode">请求时的原始代码（用于写回 <see cref="FullCashFlowRow.Code"/>）。</param>
    /// <returns>解析出的行列表（未排序）。</returns>
    /// <exception cref="DataSourceException">JSON 不含 <c>data</c> 数组。</exception>
    internal static List<FullCashFlowRow> ParseResponse(string jsonBody, string inputCode)
    {
        var rows = new List<FullCashFlowRow>();
        using var doc = JsonDocument.Parse(jsonBody);
        var root = doc.RootElement;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            throw new DataSourceException("EastMoney", "EastMoney xjllbAjaxNew response missing 'data' array.");
        }

        var normalizedCode = EastMoneyCompanyTypeResolver.NormalizeCode(inputCode);

        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var raw = EastMoneyCompanyTypeResolver.FlattenRawFields(item);
            var reportDate = EastMoneyCompanyTypeResolver.SafeParseDate(raw.GetValueOrDefault("REPORT_DATE")) ?? default;

            // EM 真实接口字段语义（v1.4.0 实测确认）：
            //   NETCASH_OPERATE  = 经营活动现金流量净额 (CFO)
            //   NETCASH_INVEST   = 投资活动现金流量净额 (CFI)
            //   NETCASH_FINANCE  = 筹资活动现金流量净额 (CFF)
            // EM 没有直接的"现金及现金等价物净增加额"字段（254 个字段实测无 MANANETR），
            // 因此 NetCashIncrease 由 CFO + CFI + CFF 派生（三者同时存在时）。
            var cfo = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("NETCASH_OPERATE"));
            var cfi = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("NETCASH_INVEST"));
            var cff = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("NETCASH_FINANCE"));
            decimal? netCashIncrease = (cfo.HasValue && cfi.HasValue && cff.HasValue)
                ? cfo.Value + cfi.Value + cff.Value
                : (decimal?)null;

#pragma warning disable CS0618 // NetcashOperate 标记 Obsolete，仍需为旧字段填值以保持兼容
            var row = new FullCashFlowRow
            {
                Code = normalizedCode,
                ReportDate = reportDate,
                ReportTitle = raw.GetValueOrDefault("REPORT_TYPE"),
                SalesServices = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("SALES_SERVICES")),
                TotalOperateInflow = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("TOTAL_OPERATE_INFLOW")),
                TotalOperateOutflow = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("TOTAL_OPERATE_OUTFLOW")),
                NetcashOperate = cfo,            // v1.4.0 起：CFO（与 v1.3.x EM 路径同值，无 BREAKING）
                NetCashIncrease = netCashIncrease, // 由 CFO+CFI+CFF 派生
                OperatingCashFlow = cfo,         // 真正的经营活动现金流量净额
                TotalInvestInflow = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("TOTAL_INVEST_INFLOW")),
                TotalInvestOutflow = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("TOTAL_INVEST_OUTFLOW")),
                NetcashInvest = cfi,
                TotalFinanceInflow = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("TOTAL_FINANCE_INFLOW")),
                TotalFinanceOutflow = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("TOTAL_FINANCE_OUTFLOW")),
                NetcashFinance = cff,
                BeginCce = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("BEGIN_CCE")),
                EndCce = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("END_CCE")),
                RawFields = raw,
                Source = "EastMoney",
            };
#pragma warning restore CS0618
            rows.Add(row);
        }

        return rows;
    }
}
