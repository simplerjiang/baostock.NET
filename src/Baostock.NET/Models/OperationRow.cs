namespace Baostock.NET.Models;

public sealed record OperationRow(
    string Code,
    string? PubDate,
    string? StatDate,
    decimal? NrTurnRatio,
    decimal? NrTurnDays,
    decimal? InvTurnRatio,
    decimal? InvTurnDays,
    decimal? CaTurnRatio,
    decimal? AssetTurnRatio);
