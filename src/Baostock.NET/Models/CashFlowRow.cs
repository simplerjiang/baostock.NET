namespace Baostock.NET.Models;

/// <summary>季频现金流量数据行。</summary>
/// <param name="Code">证券代码。</param>
/// <param name="PubDate">公告日期。</param>
/// <param name="StatDate">统计截止日期。</param>
/// <param name="CaToAsset">流动资产除以总资产。</param>
/// <param name="NcaToAsset">非流动资产除以总资产。</param>
/// <param name="TangibleAssetToAsset">有形资产除以总资产。</param>
/// <param name="EbitToInterest">已获利息倍数（EBIT/利息费用）。</param>
/// <param name="CfoToOr">经营活动现金流量净额/营业收入。</param>
/// <param name="CfoToNp">经营活动现金流量净额/净利润。</param>
/// <param name="CfoToGr">经营活动现金流量净额/营业总收入。</param>
public sealed record CashFlowRow(
    string Code,
    string? PubDate,
    string? StatDate,
    decimal? CaToAsset,
    decimal? NcaToAsset,
    decimal? TangibleAssetToAsset,
    decimal? EbitToInterest,
    decimal? CfoToOr,
    decimal? CfoToNp,
    decimal? CfoToGr);
