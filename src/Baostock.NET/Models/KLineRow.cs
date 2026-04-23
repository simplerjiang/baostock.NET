namespace Baostock.NET.Models;

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

public enum AdjustFlag { PostAdjust = 1, PreAdjust = 2, NoAdjust = 3 }

public enum TradeStatus { Suspended = 0, Normal = 1 }

public enum KLineFrequency { Day, Week, Month, FiveMinute, FifteenMinute, ThirtyMinute, SixtyMinute }
