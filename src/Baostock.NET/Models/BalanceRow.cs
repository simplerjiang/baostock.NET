namespace Baostock.NET.Models;

/// <summary>季频偿债能力数据行。</summary>
/// <param name="Code">证券代码。</param>
/// <param name="PubDate">公告日期。</param>
/// <param name="StatDate">统计截止日期。</param>
/// <param name="CurrentRatio">流动比率。</param>
/// <param name="QuickRatio">速动比率。</param>
/// <param name="CashRatio">现金比率。</param>
/// <param name="YoyLiability">负债同比增长率（%）。</param>
/// <param name="LiabilityToAsset">资产负债率。</param>
/// <param name="AssetToEquity">权益乘数。</param>
public sealed record BalanceRow(
    string Code,
    string? PubDate,
    string? StatDate,
    decimal? CurrentRatio,
    decimal? QuickRatio,
    decimal? CashRatio,
    decimal? YoyLiability,
    decimal? LiabilityToAsset,
    decimal? AssetToEquity);
