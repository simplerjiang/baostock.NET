using System.Globalization;
using System.Text.Json;
using Baostock.NET.Http;
using Baostock.NET.Models;
using Baostock.NET.Util;

namespace Baostock.NET.Cninfo;

/// <summary>
/// 巨潮资讯网（cninfo.com.cn）公告索引 + PDF 下载数据源。
/// </summary>
/// <remarks>
/// <para>接口形式：</para>
/// <list type="bullet">
///   <item><description>公告列表：<c>POST http://www.cninfo.com.cn/new/hisAnnouncement/query</c>（<c>application/x-www-form-urlencoded</c>）。</description></item>
///   <item><description>PDF：<c>GET http://static.cninfo.com.cn/{adjunctUrl}</c>，支持 <c>Range</c> 断点续传。</description></item>
/// </list>
/// <para>注入 <c>baseUri</c> / <c>pdfBaseUri</c> 便于集成测试重定向到本地 mock 服务端。</para>
/// </remarks>
public sealed class CninfoSource : ICninfoSource
{
    /// <summary>公告查询接口默认主机。</summary>
    public const string DefaultBaseUri = "http://www.cninfo.com.cn";

    /// <summary>PDF 静态资源默认主机。</summary>
    public const string DefaultPdfBaseUri = "http://static.cninfo.com.cn";

    /// <summary>公告列表查询路径。</summary>
    public const string QueryPath = "/new/hisAnnouncement/query";

    /// <summary>公告查询请求超时；默认 15 秒。</summary>
    public TimeSpan QueryTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>PDF 下载 header 超时；默认 5 分钟（body 流读取不受此超时约束，由调用方 CancellationToken 控制）。</summary>
    public TimeSpan DownloadHeaderTimeout { get; init; } = TimeSpan.FromMinutes(5);

    private readonly HttpDataClient _http;
    private readonly SourceHealthRegistry? _health;
    private readonly string _baseUri;
    private readonly string _pdfBaseUri;
    private readonly CninfoOrgIdResolver _orgIdResolver;

    /// <summary>构造一个巨潮数据源实例。</summary>
    /// <param name="http">HTTP 客户端；为 <see langword="null"/> 时使用 <see cref="HttpDataClient.Default"/>。</param>
    /// <param name="health">健康注册表；为 <see langword="null"/> 时不做健康统计。</param>
    /// <param name="baseUri">公告查询基础 URI；为 <see langword="null"/> 时使用 <see cref="DefaultBaseUri"/>。</param>
    /// <param name="pdfBaseUri">PDF 静态资源基础 URI；为 <see langword="null"/> 时使用 <see cref="DefaultPdfBaseUri"/>。</param>
    /// <param name="orgIdResolver">orgId 解析器；为 <see langword="null"/> 时按 <paramref name="http"/> / <paramref name="baseUri"/> 构造一个。</param>
    public CninfoSource(
        HttpDataClient? http = null,
        SourceHealthRegistry? health = null,
        Uri? baseUri = null,
        Uri? pdfBaseUri = null,
        CninfoOrgIdResolver? orgIdResolver = null)
    {
        _http = http ?? HttpDataClient.Default;
        _health = health;
        _baseUri = (baseUri?.ToString() ?? DefaultBaseUri).TrimEnd('/');
        _pdfBaseUri = (pdfBaseUri?.ToString() ?? DefaultPdfBaseUri).TrimEnd('/');
        _orgIdResolver = orgIdResolver ?? new CninfoOrgIdResolver(_http, baseUri);
    }

    /// <inheritdoc />
    public string Name => "Cninfo";

    /// <inheritdoc />
    public async Task<IReadOnlyList<CninfoAnnouncementRow>> QueryAnnouncementsAsync(
        CninfoAnnouncementRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var sc = CodeFormatter.Parse(request.Code);
            // 通过 topSearch/query 在线解析真实 orgId（深市创业板 / 科创板 / 部分股票的
            // orgId 并不符合 gss{h|z|b}0{6位} 拼接规则，必须查线上）。
            var orgId = await _orgIdResolver.ResolveAsync(sc.Code6, ct).ConfigureAwait(false);
            var url = _baseUri + QueryPath;
            var form = BuildQueryForm(sc, orgId, request);
            var headers = BuildQueryHeaders();
            var body = await _http.PostFormAsync(url, form, headers, QueryTimeout, ct).ConfigureAwait(false);
            var rows = ParseAnnouncements(body, request.Code);
            _health?.MarkSuccess(Name);
            return rows;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _health?.MarkFailure(Name, ex);
            if (ex is DataSourceException) throw;
            throw new DataSourceException(Name, $"Cninfo announcement query failed: {ex.Message}", null, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Stream> DownloadPdfAsync(
        string adjunctUrl,
        long? rangeStart = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(adjunctUrl))
        {
            throw new ArgumentException("adjunctUrl must not be null or empty.", nameof(adjunctUrl));
        }
        HttpResponseMessage? resp = null;
        try
        {
            var url = _pdfBaseUri + "/" + adjunctUrl.TrimStart('/');
            resp = await _http.GetStreamAsync(url, rangeStart, BuildPdfHeaders(), DownloadHeaderTimeout, ct).ConfigureAwait(false);
            var inner = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            _health?.MarkSuccess(Name);
            return new ResponseOwnedStream(resp, inner);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            resp?.Dispose();
            _health?.MarkFailure(Name, ex);
            if (ex is DataSourceException) throw;
            throw new DataSourceException(Name, $"Cninfo PDF download failed: {ex.Message}", null, ex);
        }
    }

    /// <inheritdoc />
    public async Task<long> DownloadPdfToFileAsync(
        string adjunctUrl,
        string destinationPath,
        bool resume = true,
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

        long? rangeStart = null;
        var mode = FileMode.Create;
        if (resume && File.Exists(destinationPath))
        {
            var existing = new FileInfo(destinationPath).Length;
            if (existing > 0)
            {
                rangeStart = existing;
                mode = FileMode.Append;
            }
        }

        await using var stream = await DownloadPdfAsync(adjunctUrl, rangeStart, ct).ConfigureAwait(false);
        await using (var fs = new FileStream(destinationPath, mode, FileAccess.Write, FileShare.None))
        {
            await stream.CopyToAsync(fs, ct).ConfigureAwait(false);
        }
        return new FileInfo(destinationPath).Length;
    }

    /// <summary>构造公告查询表单体。</summary>
    /// <param name="sc">标准化证券代码。</param>
    /// <param name="orgId">通过 <see cref="CninfoOrgIdResolver"/> 在线解析得到的真实 orgId。</param>
    /// <param name="request">查询请求。</param>
    /// <returns>表单字典。</returns>
    internal static Dictionary<string, string> BuildQueryForm(StockCode sc, string orgId, CninfoAnnouncementRequest request)
    {
        var pageNum = request.PageNum <= 0 ? 1 : request.PageNum;
        var pageSize = request.PageSize <= 0 ? 30 : request.PageSize;
        var seDate = (request.StartDate.HasValue || request.EndDate.HasValue)
            ? $"{FormatDate(request.StartDate)}~{FormatDate(request.EndDate)}"
            : string.Empty;
        return new Dictionary<string, string>
        {
            ["stock"] = sc.Code6 + "," + orgId,
            ["tabName"] = "fulltext",
            ["pageSize"] = pageSize.ToString(CultureInfo.InvariantCulture),
            ["pageNum"] = pageNum.ToString(CultureInfo.InvariantCulture),
            ["column"] = CodeToColumn(sc.EastMoneyForm),
            ["category"] = CategoryToParam(request.Category),
            ["plate"] = string.Empty,
            ["seDate"] = seDate,
            ["searchkey"] = string.Empty,
            ["secid"] = string.Empty,
            ["sortName"] = string.Empty,
            ["sortType"] = string.Empty,
            ["isHLtitle"] = "true",
        };
    }

    private static string FormatDate(DateOnly? d)
        => d is null ? string.Empty : d.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>巨潮公告查询通用请求头。</summary>
    /// <returns>请求头字典。</returns>
    internal static Dictionary<string, string> BuildQueryHeaders() => new()
    {
        ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        ["Accept"] = "application/json, text/plain, */*",
        ["Referer"] = "http://www.cninfo.com.cn/new/commonUrl/pageOfSearch?url=disclosure/list/search",
        ["Origin"] = "http://www.cninfo.com.cn",
        ["X-Requested-With"] = "XMLHttpRequest",
    };

    /// <summary>巨潮 PDF 下载通用请求头。</summary>
    /// <returns>请求头字典。</returns>
    internal static Dictionary<string, string> BuildPdfHeaders() => new()
    {
        ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        ["Accept"] = "application/pdf, */*",
        ["Referer"] = "http://www.cninfo.com.cn/",
    };

    /// <summary>将分类枚举映射为巨潮 <c>category</c> 参数字符串。</summary>
    /// <param name="category">分类枚举。</param>
    /// <returns>请求参数；<see cref="CninfoAnnouncementCategory.All"/> 返回空字符串。</returns>
    internal static string CategoryToParam(CninfoAnnouncementCategory category) => category switch
    {
        CninfoAnnouncementCategory.AnnualReport => "category_ndbg_szsh",
        CninfoAnnouncementCategory.SemiAnnualReport => "category_bndbg_szsh",
        CninfoAnnouncementCategory.QuarterlyReport => "category_sjdbg_szsh",
        CninfoAnnouncementCategory.PerformanceForecast => "category_yjygjxz_szsh",
        CninfoAnnouncementCategory.TemporaryAnnouncement => "category_lshgg_szsh",
        CninfoAnnouncementCategory.All => string.Empty,
        _ => string.Empty,
    };

    /// <summary>
    /// 根据东财风格代码推断巨潮 <c>column</c> 参数：
    /// <c>SH*</c> → <c>sse</c>，<c>SZ*</c> → <c>szse</c>，<c>BJ*</c> → <c>bj</c>。
    /// </summary>
    /// <param name="eastMoneyCode">东财风格代码。</param>
    /// <returns>column 参数值。</returns>
    internal static string CodeToColumn(string eastMoneyCode)
    {
        if (string.IsNullOrEmpty(eastMoneyCode) || eastMoneyCode.Length < 2)
        {
            throw new ArgumentException("Invalid east-money code.", nameof(eastMoneyCode));
        }
        var prefix = eastMoneyCode.Substring(0, 2).ToUpperInvariant();
        return prefix switch
        {
            "SH" => "sse",
            "SZ" => "szse",
            "BJ" => "bj",
            _ => throw new ArgumentException($"Unsupported exchange prefix: {prefix}", nameof(eastMoneyCode)),
        };
    }

    /// <summary>解析巨潮公告查询 JSON 响应。</summary>
    /// <param name="jsonBody">响应体。</param>
    /// <param name="originalCode">发起查询时使用的原始代码（回退用）。</param>
    /// <returns>公告条目列表。</returns>
    /// <exception cref="DataSourceException">JSON 无 <c>announcements</c> 字段或结构非法时抛出。</exception>
    internal static List<CninfoAnnouncementRow> ParseAnnouncements(string jsonBody, string originalCode)
    {
        if (string.IsNullOrWhiteSpace(jsonBody))
        {
            throw new DataSourceException("Cninfo", "Empty response body.");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(jsonBody);
        }
        catch (JsonException ex)
        {
            throw new DataSourceException("Cninfo", $"Invalid JSON: {ex.Message}", null, ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("announcements", out var arr))
            {
                throw new DataSourceException("Cninfo", "Response does not contain 'announcements' field.");
            }
            if (arr.ValueKind == JsonValueKind.Null)
            {
                return new List<CninfoAnnouncementRow>();
            }
            if (arr.ValueKind != JsonValueKind.Array)
            {
                throw new DataSourceException("Cninfo", "'announcements' is not an array.");
            }

            var rows = new List<CninfoAnnouncementRow>(arr.GetArrayLength());
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                var id = GetString(item, "announcementId");
                var title = GetString(item, "announcementTitle");
                var adjunct = GetString(item, "adjunctUrl");
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(title) || string.IsNullOrEmpty(adjunct))
                {
                    continue;
                }

                var secCode = GetString(item, "secCode");
                var secName = GetString(item, "secName");
                var orgId = GetString(item, "orgId");
                var timeMs = GetInt64(item, "announcementTime");
                var columnId = GetString(item, "columnId");

                rows.Add(new CninfoAnnouncementRow
                {
                    AnnouncementId = id!,
                    Code = ResolveEastMoneyCode(secCode, orgId, originalCode),
                    SecurityName = string.IsNullOrEmpty(secName) ? null : secName,
                    Title = title!,
                    PublishDate = ConvertUnixMsToBeijingDate(timeMs),
                    Category = string.IsNullOrEmpty(columnId) ? null : columnId,
                    AdjunctUrl = adjunct!,
                });
            }
            return rows;
        }
    }

    private static string? GetString(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var el))
        {
            return null;
        }
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => el.ToString(),
        };
    }

    private static long GetInt64(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var el))
        {
            return 0L;
        }
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetInt64(out var v) ? v : 0L,
            JsonValueKind.String when long.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) => v,
            _ => 0L,
        };
    }

    private static DateOnly ConvertUnixMsToBeijingDate(long ms)
    {
        if (ms <= 0)
        {
            return default;
        }
        var dto = DateTimeOffset.FromUnixTimeMilliseconds(ms).ToOffset(TimeSpan.FromHours(8));
        return DateOnly.FromDateTime(dto.DateTime);
    }

    private static string ResolveEastMoneyCode(string? secCode, string? orgId, string originalCode)
    {
        if (!string.IsNullOrEmpty(secCode) && !string.IsNullOrEmpty(orgId) && orgId.Length >= 4)
        {
            // orgId 形如 gssh0600519 / gssz0000001 / gssb0430047
            var ch = char.ToLowerInvariant(orgId[3]);
            var prefix = ch switch
            {
                'h' => "SH",
                'z' => "SZ",
                'b' => "BJ",
                _ => null,
            };
            if (prefix != null)
            {
                return prefix + secCode;
            }
        }
        return CodeFormatter.TryParse(originalCode, out var sc) ? sc.EastMoneyForm : originalCode;
    }

    /// <summary>
    /// 包装 <see cref="HttpResponseMessage"/> 内容流，Dispose 时同时释放 <see cref="HttpResponseMessage"/>，
    /// 让调用方只需要 dispose 返回的 <see cref="Stream"/> 即可释放整个响应。
    /// </summary>
    private sealed class ResponseOwnedStream : Stream
    {
        private readonly HttpResponseMessage _response;
        private readonly Stream _inner;

        public ResponseOwnedStream(HttpResponseMessage response, Stream inner)
        {
            _response = response;
            _inner = inner;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _inner.Dispose(); } catch { /* best effort */ }
                try { _response.Dispose(); } catch { /* best effort */ }
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            try { await _inner.DisposeAsync().ConfigureAwait(false); } catch { /* best effort */ }
            try { _response.Dispose(); } catch { /* best effort */ }
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}
