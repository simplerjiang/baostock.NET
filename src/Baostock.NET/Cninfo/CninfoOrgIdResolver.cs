using System.Collections.Concurrent;
using System.Text.Json;
using Baostock.NET.Http;

namespace Baostock.NET.Cninfo;

/// <summary>
/// 巨潮资讯网真实 <c>orgId</c> 解析器。
/// </summary>
/// <remarks>
/// <para>背景：历史版本里通过 <c>gss{h|z|b}0{6位}</c> 的拼接规则合成 <c>orgId</c>，
/// 但该规则仅对部分上交所主板 / 深交所主板命中；深市创业板（<c>300xxx</c>，如宁德时代 <c>GD165627</c>）、
/// 科创板（<c>688xxx</c>，如中芯国际 <c>gshk0000981</c>）、部分公司（如美的集团 <c>9900005965</c>）
/// 都与规则不符，导致 <c>hisAnnouncement/query</c> 返回 0 条。</para>
/// <para>本解析器通过调用
/// <c>POST http://www.cninfo.com.cn/new/information/topSearch/query?keyWord={6位}&amp;maxNum=10</c>
/// 拿到真实 orgId，并在进程内缓存（key = 6 位代码）。</para>
/// <para>解析失败（代码找不到 / API 自身 5xx / 响应无 <c>orgId</c>）一律抛
/// <see cref="DataSourceException"/>，由上层 hedge runner 判定 fail-over；
/// <b>不</b> fallback 到合成 <c>orgId</c>，避免掩盖真实故障。</para>
/// </remarks>
public sealed class CninfoOrgIdResolver
{
    /// <summary>默认巨潮主站 URI。</summary>
    public const string DefaultBaseUri = "http://www.cninfo.com.cn";

    /// <summary>topSearch 查询路径。</summary>
    public const string QueryPath = "/new/information/topSearch/query";

    /// <summary>HTTP 超时；默认 10 秒。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    private readonly HttpDataClient _http;
    private readonly string _baseUri;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);

    /// <summary>构造一个 orgId 解析器实例。</summary>
    /// <param name="http">HTTP 客户端；为 <see langword="null"/> 时使用 <see cref="HttpDataClient.Default"/>。</param>
    /// <param name="baseUri">巨潮主站基础 URI；为 <see langword="null"/> 时使用 <see cref="DefaultBaseUri"/>（测试可注入）。</param>
    public CninfoOrgIdResolver(HttpDataClient? http = null, Uri? baseUri = null)
    {
        _http = http ?? HttpDataClient.Default;
        _baseUri = (baseUri?.ToString() ?? DefaultBaseUri).TrimEnd('/');
    }

    /// <summary>解析指定 6 位证券代码对应的真实巨潮 <c>orgId</c>。</summary>
    /// <param name="code">任意格式代码（将归一化到 6 位数字）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>真实 <c>orgId</c>（如 <c>gssh0600519</c> / <c>GD165627</c> / <c>gshk0000981</c> / <c>9900005965</c>）。</returns>
    /// <exception cref="ArgumentException">代码无法归一化到 6 位数字。</exception>
    /// <exception cref="DataSourceException">API 未返回匹配 <c>orgId</c>，或 HTTP 本身失败。</exception>
    public async Task<string> ResolveAsync(string code, CancellationToken ct = default)
    {
        var code6 = Normalize(code);
        if (_cache.TryGetValue(code6, out var cached))
        {
            return cached;
        }

        string body;
        try
        {
            // topSearch/query 用 POST，keyWord 走 query string。
            var url = _baseUri + QueryPath
                + "?keyWord=" + code6
                + "&maxNum=10";
            body = await _http.PostFormAsync(
                url,
                form: new Dictionary<string, string>(),
                headers: BuildHeaders(),
                timeout: Timeout,
                ct: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DataSourceException("Cninfo", $"orgId resolve failed: {ex.Message}", null, ex);
        }

        var orgId = ParseOrgId(body, code6);
        if (string.IsNullOrEmpty(orgId))
        {
            throw new DataSourceException("Cninfo", $"orgId not found for {code6}");
        }

        _cache[code6] = orgId;
        return orgId;
    }

    /// <summary>清空缓存（仅测试用）。</summary>
    internal void ClearCache() => _cache.Clear();

    /// <summary>解析 <c>topSearch/query</c> 响应，提取与 <paramref name="code6"/> 精确匹配项的 <c>orgId</c>。</summary>
    /// <param name="jsonBody">响应正文（顶层为数组）。</param>
    /// <param name="code6">6 位证券代码。</param>
    /// <returns>匹配到的 <c>orgId</c>；未找到返回 <see langword="null"/>。</returns>
    internal static string? ParseOrgId(string jsonBody, string code6)
    {
        if (string.IsNullOrWhiteSpace(jsonBody))
        {
            return null;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(jsonBody);
        }
        catch (JsonException ex)
        {
            throw new DataSourceException("Cninfo", $"orgId resolve: invalid JSON: {ex.Message}", null, ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (!item.TryGetProperty("code", out var codeEl) || codeEl.ValueKind != JsonValueKind.String) continue;
                if (!string.Equals(codeEl.GetString(), code6, StringComparison.Ordinal)) continue;
                if (item.TryGetProperty("orgId", out var orgEl) && orgEl.ValueKind == JsonValueKind.String)
                {
                    var v = orgEl.GetString();
                    if (!string.IsNullOrEmpty(v)) return v;
                }
            }
            return null;
        }
    }

    /// <summary>把任意格式代码归一化为 6 位数字字符串。</summary>
    /// <param name="code">输入代码。</param>
    /// <returns>6 位数字字符串。</returns>
    /// <exception cref="ArgumentException">输入为空或无法提取 6 位数字。</exception>
    internal static string Normalize(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("code must not be null or empty.", nameof(code));
        }
        var t = code.Trim();
        // 先取末尾 6 位数字片段；支持 "SH600519" / "sh.600519" / "600519" / "sh600519"
        int end = t.Length;
        int start = end;
        while (start > 0 && char.IsDigit(t[start - 1])) start--;
        var digits = t.Substring(start, end - start);
        if (digits.Length == 6 && digits.All(char.IsDigit))
        {
            return digits;
        }
        // 再兜底：从整串中提取所有数字取最后 6 位
        var all = new string(t.Where(char.IsDigit).ToArray());
        if (all.Length >= 6)
        {
            return all.Substring(all.Length - 6);
        }
        throw new ArgumentException($"Cannot normalize '{code}' to 6-digit code.", nameof(code));
    }

    /// <summary>topSearch 接口通用请求头。</summary>
    /// <returns>请求头字典。</returns>
    internal static Dictionary<string, string> BuildHeaders() => new()
    {
        ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        ["Accept"] = "application/json, text/plain, */*",
        ["Referer"] = "http://www.cninfo.com.cn/",
        ["Origin"] = "http://www.cninfo.com.cn",
    };
}
