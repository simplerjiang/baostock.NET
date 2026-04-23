namespace Baostock.NET.Models;

public sealed record ForecastReportRow(
    string Code,
    string? ProfitForcastExpPubDate,
    string? ProfitForcastExpStatDate,
    string? ProfitForcastType,
    string? ProfitForcastAbstract,
    string? ProfitForcastChgPctUp,
    string? ProfitForcastChgPctDwn);
