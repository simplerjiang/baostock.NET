using System.Text.Json;
using Baostock.NET.Http;
using Baostock.NET.Models;

namespace Baostock.NET.Financials;

/// <summary>
/// 东方财富利润表数据源（<c>https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/lrbAjaxNew</c>）。
/// </summary>
/// <remarks>
/// <para>仅实现 <see cref="GetIncomeStatementAsync"/>；另外两张报表请使用
/// <see cref="EastMoneyBalanceSheetSource"/> / <see cref="EastMoneyCashFlowSource"/>。</para>
/// <para>流程：嗅探 <see cref="CompanyType"/>（若未指定）→ 拉日期列表（若未指定，默认最近 10 期）→
/// 日期按每批 5 个切分调 <c>lrbAjaxNew</c> → 合并 → 按 ReportDate 降序。</para>
/// </remarks>
public sealed class EastMoneyIncomeStatementSource : IFinancialStatementSource
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

    /// <summary>构造一个东财利润表数据源。</summary>
    /// <param name="http">HTTP 客户端；为 <see langword="null"/> 时使用 <see cref="HttpDataClient.Default"/>。</param>
    /// <param name="resolver">公司类型嗅探器；为 <see langword="null"/> 时使用 <see cref="EastMoneyCompanyTypeResolver.Default"/>。</param>
    /// <param name="health">健康注册表；为 <see langword="null"/> 时不做健康统计。</param>
    public EastMoneyIncomeStatementSource(
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
    public async Task<IReadOnlyList<FullIncomeStatementRow>> GetIncomeStatementAsync(
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
            throw new DataSourceException(Name, $"EastMoney income statement fetch failed: {ex.Message}", null, ex);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FullCashFlowRow>> GetCashFlowStatementAsync(
        FinancialStatementRequest request,
        CancellationToken ct = default)
        => throw new NotSupportedException("Use EastMoneyCashFlowSource for cash flow statement.");

    private async Task<IReadOnlyList<FullIncomeStatementRow>> FetchAsync(
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
            var dateUrl = BaseUrl + "lrbDateAjaxNew"
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
                throw new DataSourceException(Name, $"EastMoney lrbDateAjaxNew returned no dates for '{request.Code}'.");
            }
            dates = allDates.Take(DefaultRecentCount).ToList();
        }

        var all = new List<FullIncomeStatementRow>();
        foreach (var chunk in EastMoneyCompanyTypeResolver.ChunkDates(dates, 5))
        {
            var url = BaseUrl + "lrbAjaxNew"
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
            throw new DataSourceException(Name, $"EastMoney lrbAjaxNew returned no rows for '{request.Code}'.");
        }

        all.Sort((a, b) => b.ReportDate.CompareTo(a.ReportDate));
        return all;
    }

    /// <summary>解析 <c>lrbAjaxNew</c> 响应为行列表（暴露用于离线单元测试）。</summary>
    /// <param name="jsonBody">响应 JSON 正文。</param>
    /// <param name="inputCode">请求时的原始代码（用于写回 <see cref="FullIncomeStatementRow.Code"/>）。</param>
    /// <returns>解析出的行列表（未排序）。</returns>
    /// <exception cref="DataSourceException">JSON 不含 <c>data</c> 数组。</exception>
    internal static List<FullIncomeStatementRow> ParseResponse(string jsonBody, string inputCode)
    {
        var rows = new List<FullIncomeStatementRow>();
        using var doc = JsonDocument.Parse(jsonBody);
        var root = doc.RootElement;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            throw new DataSourceException("EastMoney", "EastMoney lrbAjaxNew response missing 'data' array.");
        }

        var normalizedCode = EastMoneyCompanyTypeResolver.NormalizeCode(inputCode);

        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var raw = EastMoneyCompanyTypeResolver.FlattenRawFields(item);
            var reportDate = EastMoneyCompanyTypeResolver.SafeParseDate(raw.GetValueOrDefault("REPORT_DATE")) ?? default;

            var row = new FullIncomeStatementRow
            {
                Code = normalizedCode,
                ReportDate = reportDate,
                ReportTitle = raw.GetValueOrDefault("REPORT_TYPE"),
                TotalOperateIncome = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("TOTAL_OPERATE_INCOME")),
                OperateIncome = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("OPERATE_INCOME")),
                TotalOperateCost = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("TOTAL_OPERATE_COST")),
                OperateCost = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("OPERATE_COST")),
                SaleExpense = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("SALE_EXPENSE")),
                ManageExpense = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("MANAGE_EXPENSE")),
                ResearchExpense = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("RESEARCH_EXPENSE")),
                FinanceExpense = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("FINANCE_EXPENSE")),
                OperateProfit = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("OPERATE_PROFIT")),
                TotalProfit = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("TOTAL_PROFIT")),
                IncomeTax = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("INCOME_TAX")),
                NetProfit = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("NETPROFIT")),
                ParentNetProfit = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("PARENT_NETPROFIT")),
                BasicEps = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("BASIC_EPS")),
                DilutedEps = EastMoneyCompanyTypeResolver.SafeParseDecimal(raw.GetValueOrDefault("DILUTED_EPS")),
                RawFields = raw,
                Source = "EastMoney",
            };
            rows.Add(row);
        }

        return rows;
    }
}
