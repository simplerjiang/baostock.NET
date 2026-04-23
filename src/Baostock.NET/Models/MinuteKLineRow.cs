namespace Baostock.NET.Models;

/// <summary>分钟频 K 线数据行（5/15/30/60 分钟）。</summary>
/// <param name="Date">交易日期。</param>
/// <param name="Time">该 bar 结束时间，格式 <c>YYYYMMDDHHmmssSSS</c>（17 位）。</param>
/// <param name="Code">证券代码，如 <c>sh.600000</c>。</param>
/// <param name="Open">开盘价。</param>
/// <param name="High">最高价。</param>
/// <param name="Low">最低价。</param>
/// <param name="Close">收盘价。</param>
/// <param name="Volume">成交量（股）。</param>
/// <param name="Amount">成交额（元）。</param>
/// <param name="AdjustFlag">复权类型。</param>
public sealed record MinuteKLineRow(
    DateOnly Date,
    string Time,
    string Code,
    decimal? Open,
    decimal? High,
    decimal? Low,
    decimal? Close,
    long? Volume,
    decimal? Amount,
    AdjustFlag AdjustFlag);
