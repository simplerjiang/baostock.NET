using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Baostock.NET.Http;

/// <summary>
/// HTTP 数据抓取使用的全局单例 <see cref="HttpClient"/> 包装。
/// 封装了连接池配置、自动解压缩、统一 UA 与请求级 timeout（基于 LinkedCTS）。
/// </summary>
public sealed class HttpDataClient
{
    /// <summary>默认全局共享实例。</summary>
    public static HttpDataClient Default { get; } = new HttpDataClient();

    static HttpDataClient()
    {
        // GBK / GB18030 解码（腾讯 / 新浪行情接口必用）在 net9.0 默认未注册，
        // 这里集中注册一次，避免每个 *Source 重复 boilerplate。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// 仅用于单元测试：构造一个独立的 <see cref="HttpDataClient"/> 实例（不影响 <see cref="Default"/>）。
    /// 通过 <c>InternalsVisibleTo</c> 暴露给 <c>Baostock.NET.Tests</c>。
    /// </summary>
    /// <returns>新的实例。</returns>
    internal static HttpDataClient CreateForTesting() => new HttpDataClient();

    private readonly HttpClient _http;

    private HttpDataClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 16,
        };
        _http = new HttpClient(handler);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"Baostock.NET/{BaostockInfo.Version}");
        _http.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate");
        _http.Timeout = Timeout.InfiniteTimeSpan; // 走请求级 LinkedCTS
    }

    /// <summary>
    /// 发送一个请求，并以 <paramref name="timeout"/> 强制限定该请求的最长执行时间。
    /// 触发 timeout 与外部 ct 取消都会抛出 <see cref="OperationCanceledException"/>。
    /// </summary>
    /// <remarks>
    /// 注意：本重载在方法返回前 dispose 内部 LinkedCTS，因此 <paramref name="timeout"/>
    /// 实质上只覆盖到 header 接收完成；调用方拿到 <see cref="HttpResponseMessage"/> 之后
    /// 自行读 body 时不再受 timeout 控制。如果需要 body 读取也受同一 timeout 约束，
    /// 请改用 <see cref="GetStringAsync"/> / <see cref="PostFormAsync"/> 这类便捷方法。
    /// </remarks>
    /// <param name="request">已构建好的 <see cref="HttpRequestMessage"/>。</param>
    /// <param name="timeout">请求级超时；传入 <see cref="Timeout.InfiniteTimeSpan"/> 或 <see cref="TimeSpan.Zero"/> 表示不设。</param>
    /// <param name="ct">外部取消令牌。</param>
    /// <returns>响应消息（调用方负责 dispose）。</returns>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, TimeSpan timeout, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (timeout > TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            linked.CancelAfter(timeout);
        }
        return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// GET 文本，按指定编码解码。新浪行情等 GBK 接口请显式传入 <see cref="Encoding"/>，
    /// 调用方需自行 <c>Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)</c>。
    /// </summary>
    /// <remarks>
    /// timeout 覆盖 <b>header + body</b> 全过程：内部使用单一 LinkedCTS，header 读取与
    /// body <c>ReadAsByteArrayAsync</c> 共用同一 token，避免出现「header 已收完、body 慢
    /// 读取永久挂起绕过 timeout」的情况。
    /// </remarks>
    /// <param name="url">完整 URL。</param>
    /// <param name="headers">可选附加请求头。</param>
    /// <param name="encoding">解码用编码；为 <see langword="null"/> 时使用 UTF-8。</param>
    /// <param name="timeout">请求级超时；为 <see langword="null"/> 时默认 30 秒。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>响应正文字符串。</returns>
    public async Task<string> GetStringAsync(
        string url,
        IDictionary<string, string>? headers = null,
        Encoding? encoding = null,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (headers != null)
        {
            foreach (var kv in headers)
            {
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }
        }
        var effective = timeout ?? TimeSpan.FromSeconds(30);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (effective > TimeSpan.Zero && effective != Timeout.InfiniteTimeSpan)
        {
            linked.CancelAfter(effective);
        }
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var bytes = await resp.Content.ReadAsByteArrayAsync(linked.Token).ConfigureAwait(false);
        return (encoding ?? Encoding.UTF8).GetString(bytes);
    }

    /// <summary>POST 表单（<c>application/x-www-form-urlencoded</c>），按 UTF-8 解码响应。</summary>
    /// <remarks>
    /// timeout 覆盖 <b>header + body</b> 全过程，原因同 <see cref="GetStringAsync"/>。
    /// </remarks>
    /// <param name="url">目标 URL。</param>
    /// <param name="form">表单字段。</param>
    /// <param name="headers">可选附加请求头。</param>
    /// <param name="timeout">请求级超时；为 <see langword="null"/> 时默认 30 秒。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>响应正文（UTF-8 解码）。</returns>
    public async Task<string> PostFormAsync(
        string url,
        IDictionary<string, string> form,
        IDictionary<string, string>? headers = null,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(form),
        };
        if (headers != null)
        {
            foreach (var kv in headers)
            {
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }
        }
        var effective = timeout ?? TimeSpan.FromSeconds(30);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (effective > TimeSpan.Zero && effective != Timeout.InfiniteTimeSpan)
        {
            linked.CancelAfter(effective);
        }
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// 获取流式响应（用于 PDF/大文件下载），可携带 <c>Range</c> 头实现断点续传。
    /// 调用方负责 dispose 返回的 <see cref="HttpResponseMessage"/>。
    /// </summary>
    /// <remarks>
    /// 由于本方法把 <see cref="HttpResponseMessage"/> 交给调用方继续异步读取 body，
    /// <paramref name="timeout"/> 在本方法中只覆盖到 header 接收完成；
    /// body 流式读取的超时控制需由调用方自行通过 <paramref name="ct"/> 或自定义 CTS 实现。
    /// </remarks>
    /// <param name="url">目标 URL。</param>
    /// <param name="rangeStart">Range 起始字节，可选。</param>
    /// <param name="headers">可选附加请求头。</param>
    /// <param name="timeout">请求级超时；为 <see langword="null"/> 时默认 5 分钟。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>HTTP 响应消息（已 EnsureSuccessStatusCode）。</returns>
    public async Task<HttpResponseMessage> GetStreamAsync(
        string url,
        long? rangeStart = null,
        IDictionary<string, string>? headers = null,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (headers != null)
        {
            foreach (var kv in headers)
            {
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }
        }
        if (rangeStart.HasValue)
        {
            req.Headers.Range = new RangeHeaderValue(rangeStart.Value, null);
        }
        var resp = await SendAsync(req, timeout ?? TimeSpan.FromMinutes(5), ct).ConfigureAwait(false);
        try
        {
            resp.EnsureSuccessStatusCode();
            return resp;
        }
        catch
        {
            resp.Dispose();
            throw;
        }
    }
}
