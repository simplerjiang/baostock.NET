namespace Baostock.NET.Models;

/// <summary>除权除息（股息分红）数据行。</summary>
/// <param name="Code">证券代码。</param>
/// <param name="DividPreNoticeDate">预披露日期。</param>
/// <param name="DividAgmPumDate">股东大会公告日期。</param>
/// <param name="DividPlanAnnounceDate">分红方案公告日期。</param>
/// <param name="DividPlanDate">分红方案日期。</param>
/// <param name="DividRegistDate">股权登记日。</param>
/// <param name="DividOperateDate">除权除息日。</param>
/// <param name="DividPayDate">派息日。</param>
/// <param name="DividStockMarketDate">红股上市日。</param>
/// <param name="DividCashPsBeforeTax">每股税前派息。</param>
/// <param name="DividCashPsAfterTax">每股税后派息。</param>
/// <param name="DividStocksPs">每股送红股。</param>
/// <param name="DividCashStock">派现/送转。</param>
/// <param name="DividReserveToStockPs">每股转增股本。</param>
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
