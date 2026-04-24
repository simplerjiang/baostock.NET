namespace Baostock.NET.Cninfo;

/// <summary>
/// 巨潮公告查询请求。
/// </summary>
/// <param name="Code">东财风格证券代码，如 "SH600519"。</param>
/// <param name="Category">公告分类。</param>
/// <param name="StartDate">起始日期（含）。</param>
/// <param name="EndDate">结束日期（含）。</param>
/// <param name="PageNum">页码，从 1 开始。</param>
/// <param name="PageSize">每页条数，1..50。</param>
public sealed record CninfoAnnouncementRequest(
    string Code,
    CninfoAnnouncementCategory Category = CninfoAnnouncementCategory.All,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    int PageNum = 1,
    int PageSize = 30);
