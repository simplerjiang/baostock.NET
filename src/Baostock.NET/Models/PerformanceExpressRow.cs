namespace Baostock.NET.Models;

/// <summary>业绩快报数据行。</summary>
/// <param name="Code">证券代码。</param>
/// <param name="PerformanceExpPubDate">业绩快报公告日期。</param>
/// <param name="PerformanceExpStatDate">业绩快报统计日期。</param>
/// <param name="PerformanceExpUpdateDate">业绩快报更新日期。</param>
/// <param name="PerformanceExpressTotalAsset">总资产。</param>
/// <param name="PerformanceExpressNetAsset">净资产。</param>
/// <param name="PerformanceExpressEPSChgPct">每股收益变动幅度（%）。</param>
/// <param name="PerformanceExpressROEWa">加权平均净资产收益率（%）。</param>
/// <param name="PerformanceExpressEPSDiluted">稀释每股收益。</param>
/// <param name="PerformanceExpressGRYOY">营业收入同比增长率（%）。</param>
/// <param name="PerformanceExpressOPYOY">营业利润同比增长率（%）。</param>
public sealed record PerformanceExpressRow(
    string Code,
    string? PerformanceExpPubDate,
    string? PerformanceExpStatDate,
    string? PerformanceExpUpdateDate,
    string? PerformanceExpressTotalAsset,
    string? PerformanceExpressNetAsset,
    string? PerformanceExpressEPSChgPct,
    string? PerformanceExpressROEWa,
    string? PerformanceExpressEPSDiluted,
    string? PerformanceExpressGRYOY,
    string? PerformanceExpressOPYOY);
