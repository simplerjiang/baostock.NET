namespace Baostock.NET.Models;

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
