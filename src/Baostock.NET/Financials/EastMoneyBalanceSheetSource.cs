using System.Text.Json;
using Baostock.NET.Http;
using Baostock.NET.Models;

namespace Baostock.NET.Financials;

/// <summary>
/// 东方财富资产负债表数据源（<c>https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/zcfzbAjaxNew</c>）。
/// </summary>
/// <remarks>
/// <para>仅实现 <see cref="GetBalanceSheetAsync"/>；<see cref="GetIncomeStatementAsync"/> / <see cref="GetCashFlowStatementAsync"/>
/// 抛 <see cref="NotSupportedException"/>，请分别使用 <see cref="EastMoneyIncomeStatementSource"/> / <see cref="EastMoneyCashFlowSource"/>。</para>
/// <para>流程：嗅探 <see cref="CompanyType"/>（若未指定）→ 拉日期列表（若未指定，默认最近 10 期）→
/// 日期按每批 5 个切分调 <c>zcfzbAjaxNew</c> → 合并 → 按 ReportDate 降序。</para>
/// </remarks>
public sealed class EastMoneyBalanceSheetSource : IFinancialStatementSource
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

    /// <summary>构造一个东财资产负债表数据源。</summary>
    /// <param name="http">HTTP 客户端；为 <see langword="null"/> 时使用 <see cref="HttpDataClient.Default"/>。</param>
    /// <param name="resolver">公司类型嗅探器；为 <see langword="null"/> 时使用 <see cref="EastMoneyCompanyTypeResolver.Default"/>。</param>
    /// <param name="health">健康注册表；为 <see langword="null"/> 时不做健康统计。</param>
    public EastMoneyBalanceSheetSource(
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
    public async Task<IReadOnlyList<FullBalanceSheetRow>> GetBalanceSheetAsync(
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
            throw new DataSourceException(Name, $"EastMoney balance sheet fetch failed: {ex.Message}", null, ex);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FullIncomeStatementRow>> GetIncomeStatementAsync(
        FinancialStatementRequest request,
        CancellationToken ct = default)
        => throw new NotSupportedException("Use EastMoneyIncomeStatementSource for income statement.");

    /// <inheritdoc />
    public Task<IReadOnlyList<FullCashFlowRow>> GetCashFlowStatementAsync(
        FinancialStatementRequest request,
        CancellationToken ct = default)
        => throw new NotSupportedException("Use EastMoneyCashFlowSource for cash flow statement.");

    private async Task<IReadOnlyList<FullBalanceSheetRow>> FetchAsync(
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
            var dateUrl = BaseUrl + "zcfzbDateAjaxNew"
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
                throw new DataSourceException(Name, $"EastMoney zcfzbDateAjaxNew returned no dates for '{request.Code}'.");
            }
            dates = allDates.Take(DefaultRecentCount).ToList();
        }

        var all = new List<FullBalanceSheetRow>();
        foreach (var chunk in EastMoneyCompanyTypeResolver.ChunkDates(dates, 5))
        {
            var url = BaseUrl + "zcfzbAjaxNew"
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
            throw new DataSourceException(Name, $"EastMoney zcfzbAjaxNew returned no rows for '{request.Code}'.");
        }

        all.Sort((a, b) => b.ReportDate.CompareTo(a.ReportDate));
        return all;
    }

    /// <summary>解析 <c>zcfzbAjaxNew</c> 响应为行列表（暴露用于离线单元测试）。</summary>
    /// <param name="jsonBody">响应 JSON 正文。</param>
    /// <param name="inputCode">请求时的原始代码（用于写回 <see cref="FullBalanceSheetRow.Code"/>）。</param>
    /// <returns>解析出的行列表（未排序）。</returns>
    /// <exception cref="DataSourceException">JSON 不含 <c>data</c> 数组。</exception>
    internal static List<FullBalanceSheetRow> ParseResponse(string jsonBody, string inputCode)
    {
        var rows = new List<FullBalanceSheetRow>();
        using var doc = JsonDocument.Parse(jsonBody);
        var root = doc.RootElement;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            throw new DataSourceException("EastMoney", "EastMoney zcfzbAjaxNew response missing 'data' array.");
        }

        var normalizedCode = EastMoneyCompanyTypeResolver.NormalizeCode(inputCode);

        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var raw = EastMoneyCompanyTypeResolver.FlattenRawFields(item);
            var reportDate = EastMoneyCompanyTypeResolver.SafeParseDate(raw.GetValueOrDefault("REPORT_DATE")) ?? default;

            var row = new FullBalanceSheetRow
            {
                Code = normalizedCode,
                ReportDate = reportDate,
                ReportTitle = raw.GetValueOrDefault("REPORT_TYPE"),
                MoneyCap = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("MONEY_CAP")),
                TradeFinassetNotfvtpl = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("TRADE_FINASSET_NOTFVTPL")),
                AccountsRece = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("ACCOUNTS_RECE")),
                PrepaymentRece = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("PREPAYMENT_RECE")),
                Inventory = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("INVENTORY")),
                TotalCurrentAssets = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("TOTAL_CURRENT_ASSETS")),
                FixedAsset = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("FIXED_ASSET")),
                CipTotal = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("CIP_TOTAL")),
                IntangibleAsset = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("INTANGIBLE_ASSET")),
                TotalNoncurrentAssets = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("TOTAL_NONCURRENT_ASSETS")),
                TotalAssets = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("TOTAL_ASSETS")),
                ShortLoan = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("SHORT_LOAN")),
                AccountsPayable = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("ACCOUNTS_PAYABLE")),
                PredictLiab = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("PREDICT_LIAB")),
                TotalCurrentLiab = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("TOTAL_CURRENT_LIAB")),
                LongLoan = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("LONG_LOAN")),
                TotalNoncurrentLiab = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("TOTAL_NONCURRENT_LIAB")),
                TotalLiabilities = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("TOTAL_LIABILITIES")),
                ShareCapital = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("SHARE_CAPITAL")),
                CapitalReserve = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("CAPITAL_RESERVE")),
                SurplusReserve = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("SURPLUS_RESERVE")),
                UnassignProfit = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("UNASSIGN_PROFIT")),
                TotalParentEquity = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("TOTAL_PARENT_EQUITY")),
                TotalEquity = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("TOTAL_EQUITY")),
                RawFields = raw,
                Source = "EastMoney",
            };
            rows.Add(row);
        }

        return rows;
    }
}
