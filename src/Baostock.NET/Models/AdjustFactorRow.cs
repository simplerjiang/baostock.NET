namespace Baostock.NET.Models;

public sealed record AdjustFactorRow(
    string Code,
    string? DividOperateDate,
    decimal? ForeAdjustFactor,
    decimal? BackAdjustFactor,
    decimal? AdjustFactor);
