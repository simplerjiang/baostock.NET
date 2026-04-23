namespace Baostock.NET.Models;

public sealed record GrowthRow(
    string Code,
    string? PubDate,
    string? StatDate,
    decimal? YoyEquity,
    decimal? YoyAsset,
    decimal? YoyNi,
    decimal? YoyEpsBasic,
    decimal? YoyPni);
