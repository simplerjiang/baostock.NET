namespace Baostock.NET.Models;

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
