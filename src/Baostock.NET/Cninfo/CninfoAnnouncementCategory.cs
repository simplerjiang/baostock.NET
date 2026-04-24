namespace Baostock.NET.Cninfo;

/// <summary>
/// 巨潮公告分类（对应 `category` 请求参数）。
/// </summary>
public enum CninfoAnnouncementCategory
{
    /// <summary>年报。</summary>
    AnnualReport,

    /// <summary>半年报。</summary>
    SemiAnnualReport,

    /// <summary>季报。</summary>
    QuarterlyReport,

    /// <summary>业绩预告。</summary>
    PerformanceForecast,

    /// <summary>临时公告。</summary>
    TemporaryAnnouncement,

    /// <summary>全部（不按分类过滤）。</summary>
    All,
}
