namespace Baostock.NET.Models;

/// <summary>日频/周频/月频 K 线数据行。</summary>
/// <param name="Date">交易日期。</param>
/// <param name="Code">证券代码，如 <c>sh.600000</c>。</param>
/// <param name="Open">开盘价。</param>
/// <param name="High">最高价。</param>
/// <param name="Low">最低价。</param>
/// <param name="Close">收盘价。</param>
/// <param name="PreClose">前收盘价。</param>
/// <param name="Volume">成交量（股）。</param>
/// <param name="Amount">成交额（元）。</param>
/// <param name="AdjustFlag">复权类型。</param>
/// <param name="Turn">换手率（%）。</param>
/// <param name="TradeStatus">交易状态。</param>
/// <param name="PctChg">涨跌幅（%）。</param>
/// <param name="IsST">是否 ST 股票。</param>
public sealed record KLineRow(
    DateOnly Date,
    string Code,
    decimal? Open,
    decimal? High,
    decimal? Low,
    decimal? Close,
    decimal? PreClose,
    long? Volume,
    decimal? Amount,
    AdjustFlag AdjustFlag,
    decimal? Turn,
    TradeStatus TradeStatus,
    decimal? PctChg,
    bool IsST);

/// <summary>复权类型。</summary>
public enum AdjustFlag
{
    /// <summary>后复权。</summary>
    PostAdjust = 1,
    /// <summary>前复权。</summary>
    PreAdjust = 2,
    /// <summary>不复权。</summary>
    NoAdjust = 3
}

/// <summary>交易状态。</summary>
public enum TradeStatus
{
    /// <summary>停牌。</summary>
    Suspended = 0,
    /// <summary>正常交易。</summary>
    Normal = 1
}

/// <summary>K 线频率。</summary>
public enum KLineFrequency
{
    /// <summary>日频。</summary>
    Day,
    /// <summary>周频。</summary>
    Week,
    /// <summary>月频。</summary>
    Month,
    /// <summary>5 分钟。</summary>
    FiveMinute,
    /// <summary>15 分钟。</summary>
    FifteenMinute,
    /// <summary>30 分钟。</summary>
    ThirtyMinute,
    /// <summary>60 分钟。</summary>
    SixtyMinute
}
