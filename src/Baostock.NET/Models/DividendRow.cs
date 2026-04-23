namespace Baostock.NET.Models;

public sealed record DividendRow(
    string Code,
    string? DividPreNoticeDate,
    string? DividAgmPumDate,
    string? DividPlanAnnounceDate,
    string? DividPlanDate,
    string? DividRegistDate,
    string? DividOperateDate,
    string? DividPayDate,
    string? DividStockMarketDate,
    string? DividCashPsBeforeTax,
    string? DividCashPsAfterTax,
    string? DividStocksPs,
    string? DividCashStock,
    string? DividReserveToStockPs);
