namespace Baostock.NET.Models;

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
