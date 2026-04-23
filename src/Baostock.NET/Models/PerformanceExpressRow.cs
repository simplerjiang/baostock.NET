namespace Baostock.NET.Models;

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
