using Baostock.NET.Models;

namespace Baostock.NET.Cninfo;

/// <summary>
/// 巨潮资讯网数据源接口。公告索引 + PDF 下载。
/// </summary>
public interface ICninfoSource
{
    /// <summary>源标识名。</summary>
    string Name { get; }

    /// <summary>
    /// 查询公告列表。分页语义由 <see cref="CninfoAnnouncementRequest.PageNum"/> / <see cref="CninfoAnnouncementRequest.PageSize"/> 控制。
    /// </summary>
    /// <param name="request">公告查询请求。</param>
    /// <param name="ct">取消令牌。</param>
    Task<IReadOnlyList<CninfoAnnouncementRow>> QueryAnnouncementsAsync(
        CninfoAnnouncementRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// 流式下载 PDF。调用方负责释放 <see cref="Stream"/>。
    /// 支持 <paramref name="rangeStart"/> 断点续传（传 null 表示从头下载）。
    /// </summary>
    /// <param name="adjunctUrl">PDF 相对路径。</param>
    /// <param name="rangeStart">断点续传起始字节偏移；null 表示从头下载。</param>
    /// <param name="ct">取消令牌。</param>
    Task<Stream> DownloadPdfAsync(
        string adjunctUrl,
        long? rangeStart = null,
        CancellationToken ct = default);

    /// <summary>
    /// 下载 PDF 并保存到本地文件。
    /// 若 <paramref name="destinationPath"/> 已存在，按 <paramref name="resume"/> 决定是否断点续传。
    /// </summary>
    /// <param name="adjunctUrl">PDF 相对路径。</param>
    /// <param name="destinationPath">本地保存路径。</param>
    /// <param name="resume">是否在目标文件已存在时执行断点续传。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>最终写入的总字节数。</returns>
    Task<long> DownloadPdfToFileAsync(
        string adjunctUrl,
        string destinationPath,
        bool resume = true,
        CancellationToken ct = default);
}
