namespace Baostock.NET.Models;

public sealed record DupontRow(
    string Code,
    string? PubDate,
    string? StatDate,
    decimal? DupontRoe,
    decimal? DupontAssetStoEquity,
    decimal? DupontAssetTurn,
    decimal? DupontPnitoni,
    decimal? DupontNitogr,
    decimal? DupontTaxBurden,
    decimal? DupontIntburden,
    decimal? DupontEbittogr);
