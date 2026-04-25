using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Baostock.NET.TestUI.Endpoints;

/// <summary>统一响应包装：成功返回数据 + 耗时；失败返回错误信息。</summary>
/// <param name="Sources">实际返回数据的源名称（去重）。仅 HTTP 多源对冲端点会填充；TCP / internal 端点为 null。</param>
public sealed record ApiResult(
    bool Ok,
    long ElapsedMs,
    int? RowCount = null,
    object? Data = null,
    object? Raw = null,
    string? Error = null,
    string? ErrorType = null,
    IReadOnlyList<string>? Sources = null);

/// <summary>端点字段元描述（驱动前端表单自动渲染）。</summary>
public sealed record FieldDescriptor(
    string Name,
    string Type,
    bool Required = false,
    string? Default = null,
    IReadOnlyList<string>? Options = null,
    string? Description = null);

/// <summary>端点元描述。</summary>
/// <param name="Protocol">
/// 传输协议标识：<c>"tcp"</c> = baostock TCP 长连接（单连接非线程安全，禁用 concurrency&gt;1）；
/// <c>"http"</c> = HTTP 多源 hedge（可并发）；<c>"internal"</c> = TestUI 自身管理端点（meta/loadtest）。
/// </param>
/// <param name="Method">HTTP 方法。当前注册的全部端点都是 POST，预留字段以便未来引入 GET 端点（如 PDF 下载）时区分。</param>
public sealed record EndpointDescriptor(
    string Group,
    string Name,
    string Path,
    string Description,
    IReadOnlyList<FieldDescriptor> Fields,
    string Protocol = "tcp",
    string Method = "POST");

/// <summary>用 Stopwatch 包装一次端点调用并把异常映射为 <see cref="ApiResult"/>。</summary>
internal static class EndpointRunner
{
    public static async Task<ApiResult> RunAsync(Func<Task<(int rowCount, object? data)>> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var (rowCount, data) = await action().ConfigureAwait(false);
            sw.Stop();
            return new ApiResult(Ok: true, ElapsedMs: sw.ElapsedMilliseconds, RowCount: rowCount, Data: data);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ApiResult(
                Ok: false,
                ElapsedMs: sw.ElapsedMilliseconds,
                Error: ex.Message,
                ErrorType: ex.GetType().Name);
        }
    }

    /// <summary>
    /// 与 <see cref="RunAsync(Func{Task{ValueTuple{int, object}}})"/> 相同，但额外接受一个 sources 提取器：
    /// 成功路径下从返回 data 抽取数据源名称（HTTP 多源对冲端点用：暴露最终赢源到 envelope，便于观测对冲）。
    /// 失败路径不调用 extractor，<c>Sources</c> 留空。
    /// </summary>
    public static async Task<ApiResult> RunAsync(
        Func<Task<(int rowCount, object? data)>> action,
        Func<object?, IReadOnlyList<string>?>? sourcesExtractor)
    {
        if (sourcesExtractor is null)
        {
            return await RunAsync(action).ConfigureAwait(false);
        }
        var sw = Stopwatch.StartNew();
        try
        {
            var (rowCount, data) = await action().ConfigureAwait(false);
            sw.Stop();
            IReadOnlyList<string>? sources = null;
            try { sources = sourcesExtractor(data); }
            catch { /* extractor 异常不应污染响应 */ }
            return new ApiResult(
                Ok: true,
                ElapsedMs: sw.ElapsedMilliseconds,
                RowCount: rowCount,
                Data: data,
                Sources: sources);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ApiResult(
                Ok: false,
                ElapsedMs: sw.ElapsedMilliseconds,
                Error: ex.Message,
                ErrorType: ex.GetType().Name);
        }
    }

    /// <summary>
    /// 反射式抽取 <c>data</c>（应为 <see cref="IEnumerable"/>）中每行的 <c>Source</c> 字符串属性，去重后返回。
    /// 设计为对 row 类型零依赖（避免 TestUI 引入额外接口约束），仅在第一行做一次属性查找并缓存。
    /// 若 row 类型没有 <c>Source</c> 属性或集合为空则返回 null。
    /// </summary>
    public static IReadOnlyList<string>? ExtractSourcesFromRows(object? data)
    {
        if (data is not IEnumerable enumerable) return null;
        var seen = new List<string>();
        var seenSet = new HashSet<string>(StringComparer.Ordinal);
        PropertyInfo? prop = null;
        Type? lastType = null;
        foreach (var row in enumerable)
        {
            if (row is null) continue;
            var t = row.GetType();
            if (!ReferenceEquals(t, lastType))
            {
                prop = t.GetProperty("Source", BindingFlags.Public | BindingFlags.Instance);
                lastType = t;
                if (prop is null || prop.PropertyType != typeof(string)) return null;
            }
            if (prop!.GetValue(row) is string s && !string.IsNullOrEmpty(s) && seenSet.Add(s))
            {
                seen.Add(s);
            }
        }
        return seen.Count == 0 ? null : seen;
    }

    /// <summary>把 IAsyncEnumerable 拉成 List，返回 (count, list)。</summary>
    public static async Task<(int rowCount, object? data)> DrainAsync<T>(IAsyncEnumerable<T> source, CancellationToken ct)
    {
        var list = new List<T>();
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            list.Add(item);
        }
        return (list.Count, list);
    }

    /// <summary>
    /// 大小写不敏感地在 JsonElement 对象上查找属性。
    /// 修复点：前端/调用方常用驼峰、PascalCase 或全小写交错（如 <c>adjustflag</c> vs <c>adjustFlag</c>），
    /// 直接 <see cref="JsonElement.TryGetProperty(string, out JsonElement)"/> 大小写敏感会导致字段被静默忽略走默认值。
    /// </summary>
    private static bool TryGetPropertyCI(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.ValueKind == JsonValueKind.Object)
        {
            // 先按精确匹配快路径，命中则避免遍历开销。
            if (obj.TryGetProperty(name, out value))
            {
                return true;
            }
            foreach (var p in obj.EnumerateObject())
            {
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = p.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    public static string? GetString(JsonElement body, string name)
    {
        if (body.ValueKind != JsonValueKind.Object) return null;
        if (!TryGetPropertyCI(body, name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Null) return null;
        if (v.ValueKind == JsonValueKind.String)
        {
            var s = v.GetString();
            return string.IsNullOrEmpty(s) ? null : s;
        }
        return v.ToString();
    }

    public static int GetInt(JsonElement body, string name, int fallback)
    {
        if (body.ValueKind != JsonValueKind.Object) return fallback;
        if (!TryGetPropertyCI(body, name, out var v)) return fallback;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var s)) return s;
        return fallback;
    }

    public static TEnum GetEnum<TEnum>(JsonElement body, string name, TEnum fallback) where TEnum : struct, Enum
    {
        var s = GetString(body, name);
        if (s is null) return fallback;
        return Enum.TryParse<TEnum>(s, ignoreCase: true, out var v) ? v : fallback;
    }

    public static string[] GetStringArray(JsonElement body, string name)
    {
        if (body.ValueKind != JsonValueKind.Object) return Array.Empty<string>();
        if (!TryGetPropertyCI(body, name, out var v)) return Array.Empty<string>();
        if (v.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>(v.GetArrayLength());
            foreach (var el in v.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String)
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
                }
            }
            return list.ToArray();
        }
        if (v.ValueKind == JsonValueKind.String)
        {
            var s = v.GetString() ?? string.Empty;
            return s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        return Array.Empty<string>();
    }
}
