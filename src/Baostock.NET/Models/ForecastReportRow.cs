namespace Baostock.NET.Models;

/// <summary>业绩预告数据行。</summary>
/// <param name="Code">证券代码。</param>
/// <param name="ProfitForcastExpPubDate">业绩预告公告日期。</param>
/// <param name="ProfitForcastExpStatDate">业绩预告统计日期。</param>
/// <param name="ProfitForcastType">预告类型（预增/预减/扶亏/首亏等）。</param>
/// <param name="ProfitForcastAbstract">预告摘要。</param>
/// <param name="ProfitForcastChgPctUp">变动幅度上限（%）。</param>
/// <param name="ProfitForcastChgPctDwn">变动幅度下限（%）。</param>
public sealed record ForecastReportRow(
    string Code,
    string? ProfitForcastExpPubDate,
    string? ProfitForcastExpStatDate,
    string? ProfitForcastType,
    string? ProfitForcastAbstract,
    string? ProfitForcastChgPctUp,
    string? ProfitForcastChgPctDwn);
