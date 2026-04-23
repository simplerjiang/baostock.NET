namespace Baostock.NET.Models;

/// <summary>分钟频 K 线数据行（5/15/30/60 分钟）。</summary>
public sealed record MinuteKLineRow(
    DateOnly Date,
    string Time,    // "YYYYMMDDHHmmssSSS" 17位，表示该bar的结束时间
    string Code,
    decimal? Open,
    decimal? High,
    decimal? Low,
    decimal? Close,
    long? Volume,
    decimal? Amount,
    AdjustFlag AdjustFlag);
