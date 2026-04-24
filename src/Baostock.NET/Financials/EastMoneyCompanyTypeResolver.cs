using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Baostock.NET.Http;
using Baostock.NET.Util;

namespace Baostock.NET.Financials;

/// <summary>
/// 东方财富财报 <see cref="CompanyType"/> 嗅探器。
/// </summary>
/// <remarks>
/// <para>策略：GET <c>https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/Index?type=web&amp;code={code}</c>，
/// 从返回 HTML 中正则提取 <c>&lt;input id="hidctype" value="{N}"&gt;</c>（1..4）。
/// 未匹配、HTTP 失败或网络异常一律 fallback 到 <see cref="CompanyType.General"/>。</para>
/// <para>内置进程内缓存：key 为东财风格代码（如 <c>SH600519</c>），同一进程内一次解析永久复用，避免重复嗅探。</para>
/// <para>也作为本目录下 EastMoney 三张财报源的公共 helper 载体（<see cref="SafeParseDecimal"/> / <see cref="ChunkDates"/> / <see cref="JsonValueAsString"/> 等 internal 方法）。</para>
/// </remarks>
public sealed class EastMoneyCompanyTypeResolver
{
    /// <summary>默认全局共享实例（使用 <see cref="HttpDataClient.Default"/>）。</summary>
    public static EastMoneyCompanyTypeResolver Default { get; } = new EastMoneyCompanyTypeResolver();

    /// <summary>嗅探接口 base URL。</summary>
    public const string IndexUrlBase = "https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/Index";

    /// <summary>东财 F10 相关接口必须的 Referer 头值。</summary>
    public const string RequiredReferer = "https://emweb.securities.eastmoney.com/";

    /// <summary>东财 F10 接口常用 UA（避免部分节点 403）。</summary>
    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    /// <summary>HTTP 请求超时；默认 10 秒。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    private readonly HttpDataClient _http;
    private readonly ConcurrentDictionary<string, CompanyType> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>构造一个公司类型嗅探器实例。</summary>
    /// <param name="http">HTTP 客户端；为 <see langword="null"/> 时使用 <see cref="HttpDataClient.Default"/>。</param>
    public EastMoneyCompanyTypeResolver(HttpDataClient? http = null)
    {
        _http = http ?? HttpDataClient.Default;
    }

    /// <summary>嗅探指定代码的 <see cref="CompanyType"/>。</summary>
    /// <param name="code">任意格式的证券代码（<see cref="CodeFormatter"/> 可识别的格式）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>公司类型；网络/解析失败时回落到 <see cref="CompanyType.General"/>。</returns>
    public async Task<CompanyType> ResolveAsync(string code, CancellationToken ct = default)
    {
        var sc = CodeFormatter.Parse(code);
        var key = sc.EastMoneyForm;
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var url = IndexUrlBase + "?type=web&code=" + sc.LowercaseNoDot;
        var headers = BuildHeaders(accept: "text/html");

        string html;
        try
        {
            html = await _http.GetStringAsync(url, headers, encoding: null, timeout: Timeout, ct: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // timeout 触发的 OCE：回落 General，不抛
            _cache[key] = CompanyType.General;
            return CompanyType.General;
        }
        catch (HttpRequestException)
        {
            _cache[key] = CompanyType.General;
            return CompanyType.General;
        }

        var result = ParseCompanyType(html);
        _cache[key] = result;
        return result;
    }

    /// <summary>清除内置缓存（仅测试/诊断使用）。</summary>
    internal void ClearCache() => _cache.Clear();

    /// <summary>从 HTML 中提取 <c>hidctype</c> 隐藏域的值。</summary>
    /// <param name="html">HTML 文本。</param>
    /// <returns>识别到的 <see cref="CompanyType"/>；未识别返回 <see cref="CompanyType.General"/>。</returns>
    internal static CompanyType ParseCompanyType(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return CompanyType.General;
        }

        // 允许属性顺序互换 & 引号类型
        var m = Regex.Match(
            html,
            "id\\s*=\\s*[\"']hidctype[\"'][^>]*value\\s*=\\s*[\"'](\\d)[\"']",
            RegexOptions.IgnoreCase);
        if (!m.Success)
        {
            m = Regex.Match(
                html,
                "value\\s*=\\s*[\"'](\\d)[\"'][^>]*id\\s*=\\s*[\"']hidctype[\"']",
                RegexOptions.IgnoreCase);
        }
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n) && Enum.IsDefined(typeof(CompanyType), n))
        {
            return (CompanyType)n;
        }
        return CompanyType.General;
    }

    // ====================== 下面为同目录 EastMoney 三张报表源共用 helper ======================

    /// <summary>构造东财 F10 接口通用请求头。</summary>
    /// <param name="accept">期望的 Accept 值。</param>
    /// <returns>请求头字典。</returns>
    internal static Dictionary<string, string> BuildHeaders(string accept = "application/json") => new()
    {
        ["Referer"] = RequiredReferer,
        ["User-Agent"] = UserAgent,
        ["Accept"] = accept,
    };

    /// <summary>容错解析 decimal：空 / 空白 / <c>"-"</c> / <c>"--"</c> / 非法 → <see langword="null"/>。</summary>
    /// <param name="s">待解析字符串。</param>
    /// <returns>解析成功返回 decimal；否则 <see langword="null"/>。</returns>
    internal static decimal? SafeParseDecimal(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        if (t == "-" || t == "--") return null;
        return decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    /// <summary>容错解析日期字符串（如 <c>"2024-12-31 00:00:00"</c>）为 <see cref="DateOnly"/>。</summary>
    /// <param name="s">待解析字符串。</param>
    /// <returns>解析成功返回值；否则 <see langword="null"/>。</returns>
    internal static DateOnly? SafeParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
        {
            return DateOnly.FromDateTime(dt);
        }
        return null;
    }

    /// <summary>将 <see cref="JsonElement"/> 值转为字符串（数字保留原始文本，避免精度损失）。</summary>
    /// <param name="e">JSON 节点。</param>
    /// <returns>字符串表示；null 节点返回 <see langword="null"/>。</returns>
    internal static string? JsonValueAsString(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.Undefined => null,
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => e.GetRawText(),
    };

    /// <summary>将日期序列切为每组 <paramref name="chunkSize"/> 个。</summary>
    /// <param name="dates">输入日期序列。</param>
    /// <param name="chunkSize">分块大小；默认 5（东财限制）。</param>
    /// <returns>分块后的日期列表序列。</returns>
    internal static IEnumerable<IReadOnlyList<DateOnly>> ChunkDates(IEnumerable<DateOnly> dates, int chunkSize = 5)
    {
        if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));
        var buffer = new List<DateOnly>(chunkSize);
        foreach (var d in dates)
        {
            buffer.Add(d);
            if (buffer.Count == chunkSize)
            {
                yield return buffer;
                buffer = new List<DateOnly>(chunkSize);
            }
        }
        if (buffer.Count > 0) yield return buffer;
    }

    /// <summary>将日期列表拼成东财 <c>dates</c> 参数值（<c>yyyy-MM-dd,yyyy-MM-dd,...</c>）。</summary>
    /// <param name="dates">日期集合。</param>
    /// <returns>拼接后的字符串。</returns>
    internal static string FormatDatesParam(IEnumerable<DateOnly> dates)
        => string.Join(',', dates.Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

    /// <summary>解析 <c>zcfzbDateAjaxNew</c> / <c>lrbDateAjaxNew</c> / <c>xjllbDateAjaxNew</c> 返回的日期列表。</summary>
    /// <param name="jsonBody">响应 JSON 正文。</param>
    /// <returns>日期列表（按接口返回顺序，一般为由近到远）。</returns>
    internal static List<DateOnly> ParseDateList(string jsonBody)
    {
        var list = new List<DateOnly>();
        using var doc = JsonDocument.Parse(jsonBody);
        var root = doc.RootElement;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return list;
        }
        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (!item.TryGetProperty("REPORT_DATE", out var rd)) continue;
            var s = JsonValueAsString(rd);
            var d = SafeParseDate(s);
            if (d.HasValue) list.Add(d.Value);
        }
        return list;
    }

    /// <summary>规范化请求代码到东财风格（解析失败则原样返回）。</summary>
    /// <param name="anyForm">任意风格代码。</param>
    /// <returns>东财风格字符串。</returns>
    internal static string NormalizeCode(string anyForm)
    {
        return CodeFormatter.TryParse(anyForm, out var sc) ? sc.EastMoneyForm : anyForm;
    }

    /// <summary>将一行 JSON object 的所有字段平展为字符串字典。</summary>
    /// <param name="item">JSON object 节点。</param>
    /// <returns>字段名 → 字符串值（可能为 <see langword="null"/>）的字典。</returns>
    internal static Dictionary<string, string?> FlattenRawFields(JsonElement item)
    {
        var raw = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var prop in item.EnumerateObject())
        {
            raw[prop.Name] = JsonValueAsString(prop.Value);
        }
        return raw;
    }
}
