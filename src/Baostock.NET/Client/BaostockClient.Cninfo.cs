using Baostock.NET.Cninfo;
using Baostock.NET.Models;

namespace Baostock.NET.Client;

/// <summary>
/// <see cref="BaostockClient"/> 巨潮公告扩展（v1.3.0 Sprint 3）。
/// 单源：<see cref="CninfoSource"/>。不走对冲。
/// </summary>
public sealed partial class BaostockClient
{
    /// <summary>
    /// 查询巨潮公告列表。
    /// </summary>
    /// <param name="request">公告查询请求，不能为 <see langword="null"/>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>公告行集合（按巨潮默认排序）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="Http.DataSourceException">巨潮接口调用失败。</exception>
    public Task<IReadOnlyList<CninfoAnnouncementRow>> QueryAnnouncementsAsync(
        CninfoAnnouncementRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new CninfoSource().QueryAnnouncementsAsync(request, ct);
    }

    /// <summary>
    /// 流式下载巨潮公告 PDF。返回的 <see cref="Stream"/> 由调用方释放。
    /// </summary>
    /// <param name="adjunctUrl">PDF 相对路径（不含 host），不能为空白。</param>
    /// <param name="rangeStart">断点续传起始字节偏移；<see langword="null"/> 表示从头下载。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>PDF 数据流。</returns>
    /// <exception cref="ArgumentException"><paramref name="adjunctUrl"/> 为 <see langword="null"/> 或空白。</exception>
    /// <exception cref="Http.DataSourceException">巨潮 PDF 下载失败。</exception>
    public Task<Stream> DownloadPdfAsync(
        string adjunctUrl,
        long? rangeStart = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(adjunctUrl))
        {
            throw new ArgumentException("adjunctUrl must not be null or empty.", nameof(adjunctUrl));
        }
        return new CninfoSource().DownloadPdfAsync(adjunctUrl, rangeStart, ct);
    }

    /// <summary>
    /// 下载巨潮公告 PDF 并写入本地文件，支持断点续传。
    /// </summary>
    /// <param name="adjunctUrl">PDF 相对路径，不能为空白。</param>
    /// <param name="destinationPath">本地保存路径，不能为空白。</param>
    /// <param name="resume">目标文件已存在时是否断点续传；缺省为 <see langword="false"/>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>最终写入的总字节数。</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="adjunctUrl"/> 或 <paramref name="destinationPath"/> 为 <see langword="null"/> 或空白。
    /// </exception>
    /// <exception cref="Http.DataSourceException">巨潮 PDF 下载失败。</exception>
    public Task<long> DownloadPdfToFileAsync(
        string adjunctUrl,
        string destinationPath,
        bool resume = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(adjunctUrl))
        {
            throw new ArgumentException("adjunctUrl must not be null or empty.", nameof(adjunctUrl));
        }
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("destinationPath must not be null or empty.", nameof(destinationPath));
        }
        return new CninfoSource().DownloadPdfToFileAsync(adjunctUrl, destinationPath, resume, ct);
    }
}
