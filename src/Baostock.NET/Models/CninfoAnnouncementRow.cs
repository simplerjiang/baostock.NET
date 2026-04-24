namespace Baostock.NET.Models;

/// <summary>
/// 巨潮资讯网公告索引条目。
/// </summary>
public sealed record CninfoAnnouncementRow
{
    /// <summary>公告 ID（巨潮内部标识）。</summary>
    public required string AnnouncementId { get; init; }

    /// <summary>东财风格证券代码。</summary>
    public required string Code { get; init; }

    /// <summary>证券简称。</summary>
    public string? SecurityName { get; init; }

    /// <summary>公告标题。</summary>
    public required string Title { get; init; }

    /// <summary>发布日期。</summary>
    public required DateOnly PublishDate { get; init; }

    /// <summary>公告类型（原文字符串，如 "年报"/"半年报"）。</summary>
    public string? Category { get; init; }

    /// <summary>PDF 相对路径（用于拼接下载 URL）。</summary>
    public required string AdjunctUrl { get; init; }

    /// <summary>PDF 完整下载 URL。</summary>
    public string FullPdfUrl => $"http://static.cninfo.com.cn/{AdjunctUrl}";
}
