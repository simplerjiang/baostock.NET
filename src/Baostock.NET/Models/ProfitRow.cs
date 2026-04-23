namespace Baostock.NET.Models;

/// <summary>季频盈利能力数据行。</summary>
/// <param name="Code">证券代码。</param>
/// <param name="PubDate">公告日期。</param>
/// <param name="StatDate">统计截止日期。</param>
/// <param name="RoeAvg">净资产收益率（平均）（%）。</param>
/// <param name="NpMargin">销售净利率（%）。</param>
/// <param name="GpMargin">销售毛利率（%）。</param>
/// <param name="NetProfit">净利润（元）。</param>
/// <param name="EpsTtm">每股收益（TTM）。</param>
/// <param name="MbRevenue">主营营业收入（元）。</param>
/// <param name="TotalShare">总股本。</param>
/// <param name="LiqaShare">流通股本。</param>
public sealed record ProfitRow(
    string Code,
    string? PubDate,
    string? StatDate,
    decimal? RoeAvg,
    decimal? NpMargin,
    decimal? GpMargin,
    decimal? NetProfit,
    decimal? EpsTtm,
    decimal? MbRevenue,
    decimal? TotalShare,
    decimal? LiqaShare);
