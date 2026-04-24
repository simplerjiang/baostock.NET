using Baostock.NET.Models;

namespace Baostock.NET.KLine;

/// <summary>
/// 历史 K 线请求参数（用于 <see cref="EastMoneyKLineSource"/> / <see cref="TencentKLineSource"/>）。
/// </summary>
/// <param name="Code">东方财富风格代码（亦兼容 <c>sh.600519</c> / <c>sh600519</c>）。</param>
/// <param name="Frequency">K 线频率（日/周/月/5/15/30/60 分钟）。</param>
/// <param name="StartDate">起始日期（含）。</param>
/// <param name="EndDate">结束日期（含）。</param>
/// <param name="Adjust">复权方式。语义对齐东财：
/// <see cref="AdjustFlag.PreAdjust"/>=前复权（fqt=1 / qfq）、
/// <see cref="AdjustFlag.PostAdjust"/>=后复权（fqt=2 / hfq）、
/// <see cref="AdjustFlag.NoAdjust"/>=不复权（fqt=0 / 空）。</param>
public sealed record KLineRequest(
    string Code,
    KLineFrequency Frequency,
    System.DateTime StartDate,
    System.DateTime EndDate,
    AdjustFlag Adjust);

/// <summary>K 线频率与各源参数映射的内部辅助。</summary>
internal static class KLineFrequencyMapping
{
    /// <summary>东方财富 <c>klt</c> 参数。</summary>
    public static string ToEastMoneyKlt(KLineFrequency f) => f switch
    {
        KLineFrequency.Day => "101",
        KLineFrequency.Week => "102",
        KLineFrequency.Month => "103",
        KLineFrequency.FiveMinute => "5",
        KLineFrequency.FifteenMinute => "15",
        KLineFrequency.ThirtyMinute => "30",
        KLineFrequency.SixtyMinute => "60",
        _ => throw new System.ArgumentOutOfRangeException(nameof(f), f, "未知 K 线频率"),
    };

    /// <summary>东方财富 <c>fqt</c> 参数。</summary>
    public static string ToEastMoneyFqt(AdjustFlag a) => a switch
    {
        AdjustFlag.NoAdjust => "0",
        AdjustFlag.PreAdjust => "1",
        AdjustFlag.PostAdjust => "2",
        _ => throw new System.ArgumentOutOfRangeException(nameof(a), a, "未知复权方式"),
    };

    /// <summary>腾讯周期片段（<c>day</c> / <c>week</c> / <c>month</c> / <c>m5</c>...）。</summary>
    public static string ToTencentPeriod(KLineFrequency f) => f switch
    {
        KLineFrequency.Day => "day",
        KLineFrequency.Week => "week",
        KLineFrequency.Month => "month",
        KLineFrequency.FiveMinute => "m5",
        KLineFrequency.FifteenMinute => "m15",
        KLineFrequency.ThirtyMinute => "m30",
        KLineFrequency.SixtyMinute => "m60",
        _ => throw new System.ArgumentOutOfRangeException(nameof(f), f, "未知 K 线频率"),
    };

    /// <summary>腾讯复权片段（<c>qfq</c> / <c>hfq</c> / 空）。</summary>
    public static string ToTencentFq(AdjustFlag a) => a switch
    {
        AdjustFlag.NoAdjust => string.Empty,
        AdjustFlag.PreAdjust => "qfq",
        AdjustFlag.PostAdjust => "hfq",
        _ => throw new System.ArgumentOutOfRangeException(nameof(a), a, "未知复权方式"),
    };

    /// <summary>是否分钟级频率。</summary>
    public static bool IsIntraday(KLineFrequency f) => f
        is KLineFrequency.FiveMinute
        or KLineFrequency.FifteenMinute
        or KLineFrequency.ThirtyMinute
        or KLineFrequency.SixtyMinute;
}
