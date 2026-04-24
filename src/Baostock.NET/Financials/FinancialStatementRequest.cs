namespace Baostock.NET.Financials;

/// <summary>
/// 财报查询请求（通用于 3 张报表）。
/// </summary>
/// <param name="Code">东财风格证券代码，如 "SH600519"。</param>
/// <param name="ReportDates">报告期列表；为空时让数据源自动拉取最近若干期。</param>
/// <param name="DateType">日期汇总类型（默认 <see cref="FinancialReportDateType.ByReport"/>）。</param>
/// <param name="ReportKind">报表口径（默认 <see cref="FinancialReportKind.Cumulative"/>）。</param>
/// <param name="CompanyType">公司类型。null 时由数据源自动嗅探。</param>
public sealed record FinancialStatementRequest(
    string Code,
    IReadOnlyList<DateOnly>? ReportDates = null,
    FinancialReportDateType DateType = FinancialReportDateType.ByReport,
    FinancialReportKind ReportKind = FinancialReportKind.Cumulative,
    CompanyType? CompanyType = null);
