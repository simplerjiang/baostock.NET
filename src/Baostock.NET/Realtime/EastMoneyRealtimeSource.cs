using System.Globalization;
using System.Text.Json;
using Baostock.NET.Http;
using Baostock.NET.Models;
using Baostock.NET.Util;

namespace Baostock.NET.Realtime;

/// <summary>
/// 东方财富实时行情数据源 (<c>https://push2.eastmoney.com/api/qt/stock/get</c>)。
/// </summary>
/// <remarks>
/// <para>响应：<c>{"rc":0,"data":{"f43":144541,"f152":2,...}}</c>，UTF-8 / JSON。</para>
/// <para>关键字段（详见 <c>docs/v1.2.0-source-mapping.md</c> §2 表 1）：
/// <c>f43</c>=现价、<c>f44</c>=最高、<c>f45</c>=最低、<c>f46</c>=今开、<c>f47</c>=成交量(<b>手</b>)、
/// <c>f48</c>=成交额(元)、<c>f57</c>=代码、<c>f58</c>=名称、<c>f60</c>=昨收、<c>f86</c>=Unix秒、<c>f152</c>=价格小数位（默认 2）。
/// 价格类字段都是 <b>整数 × 10^-f152</b>，必须按 f152 反归一化，禁止硬编码 ÷100。</para>
/// <para>Header：必带 <c>Referer: https://quote.eastmoney.com/</c> + <c>ut</c> token，否则易 502 / rc=102。</para>
/// <para>失败：<c>data == null</c> 或缺关键字段抛 <see cref="DataSourceException"/>(<c>code="EMPTY"</c>)。</para>
/// <para><b>批量方式</b>：当前实现按单 secid 并发 N 次调用单接口，简单稳健；
/// Sprint 0 未验证 <c>ulist.np/get</c> 批量端点，留待 Sprint 3 补 fixture 后再切换。</para>
/// <para><b>北交所 secid</b>：当前按惯例使用 <c>116.{code}</c> 前缀（<c>0.{code}</c>=SZ、<c>1.{code}</c>=SH）；
/// 该值在 Sprint 0 未做线上采样，BJ 实测留给本 Sprint 集成测试自检。</para>
/// </remarks>
public sealed class EastMoneyRealtimeSource : IDataSource<string[], IReadOnlyList<RealtimeQuote>>
{
    /// <summary>请求超时。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(8);

    /// <summary>东财 ut token，用于减小 502 概率。</summary>
    public const string DefaultUt = "fa5fd1943c7b386f172d6893dbfba10b";

    /// <summary>东财行情接口必须的 Referer。</summary>
    public const string RequiredReferer = "https://quote.eastmoney.com/";

    private static readonly string[] Fields =
    {
        "f43", "f44", "f45", "f46", "f47", "f48",
        "f57", "f58", "f60", "f86", "f116", "f117",
        "f152", "f161", "f162", "f167", "f168", "f169", "f170",
    };

    private readonly HttpDataClient _http;

    /// <summary>构造一个东方财富实时行情数据源。</summary>
    /// <param name="http">HTTP 抓取客户端，缺省使用 <see cref="HttpDataClient.Default"/>。</param>
    public EastMoneyRealtimeSource(HttpDataClient? http = null)
    {
        _http = http ?? HttpDataClient.Default;
    }

    /// <inheritdoc />
    public string Name => "EastMoney";

    /// <inheritdoc />
    public int Priority => 2;

    /// <inheritdoc />
    public async Task<IReadOnlyList<RealtimeQuote>> FetchAsync(string[] request, CancellationToken cancellationToken)
    {
        if (request is null || request.Length == 0)
        {
            throw new ArgumentException("codes must not be empty.", nameof(request));
        }

        // Sprint 2 临时方案：并发 N 次单 secid 请求；Sprint 3 切换到 ulist.np/get 批量端点。
        var tasks = new Task<RealtimeQuote>[request.Length];
        for (int i = 0; i < request.Length; i++)
        {
            tasks[i] = FetchSingleAsync(request[i], cancellationToken);
        }
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results;
    }

    private async Task<RealtimeQuote> FetchSingleAsync(string code, CancellationToken ct)
    {
        var secid = ResolveSecId(code);
        var url = "https://push2.eastmoney.com/api/qt/stock/get"
            + "?secid=" + secid
            + "&ut=" + DefaultUt
            + "&fields=" + string.Join(',', Fields)
            + "&fltt=2&invt=2";
        var headers = new Dictionary<string, string> { ["Referer"] = RequiredReferer };
        var json = await _http.GetStringAsync(url, headers, encoding: null, timeout: Timeout, ct: ct).ConfigureAwait(false);
        return ParseSingle(json, code);
    }

    internal static RealtimeQuote ParseSingle(string json, string requestedCode)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            throw new DataSourceException("EastMoney", $"EastMoney returned no data for '{requestedCode}'.");
        }
        // 必备字段：f43 现价、f57 代码、f58 名称、f152 小数位
        if (!data.TryGetProperty("f43", out var f43) || f43.ValueKind == JsonValueKind.Null)
        {
            throw new DataSourceException("EastMoney", $"EastMoney data missing f43 (price) for '{requestedCode}'.");
        }
        if (!data.TryGetProperty("f57", out var f57) || f57.ValueKind == JsonValueKind.Null)
        {
            throw new DataSourceException("EastMoney", $"EastMoney data missing f57 (code) for '{requestedCode}'.");
        }

        int decimals = data.TryGetProperty("f152", out var f152) && f152.ValueKind == JsonValueKind.Number
            ? f152.GetInt32() : 2;
        decimal scale = (decimal)Math.Pow(10, decimals);

        decimal last = NormalizePrice(f43, scale);
        decimal high = NormalizePrice(GetOrThrow(data, "f44", requestedCode), scale);
        decimal low = NormalizePrice(GetOrThrow(data, "f45", requestedCode), scale);
        decimal open = NormalizePrice(GetOrThrow(data, "f46", requestedCode), scale);
        decimal preClose = NormalizePrice(GetOrThrow(data, "f60", requestedCode), scale);
        // f47 = 成交量 (手) → 股 ×100
        long volLots = GetOrThrow(data, "f47", requestedCode).GetInt64();
        long volume = checked(volLots * 100L);
        // f48 = 成交额 (元) 浮点
        decimal amount = (decimal)GetOrThrow(data, "f48", requestedCode).GetDouble();
        // f86 = Unix 秒（北京时间需 +8）
        long unixSec = GetOrThrow(data, "f86", requestedCode).GetInt64();
        var ts = DateTime.SpecifyKind(
            DateTimeOffset.FromUnixTimeSeconds(unixSec).ToOffset(TimeSpan.FromHours(8)).DateTime,
            DateTimeKind.Unspecified);

        string name = f57.ValueKind == JsonValueKind.String
            ? data.GetProperty("f58").GetString() ?? string.Empty
            : string.Empty;
        string rawCode6 = f57.GetString() ?? string.Empty;
        string code = ResolveResponseCode(rawCode6, requestedCode);

        // 五档买卖一价：最近字段（f191/f192=委差/委比，非买卖一价；五档实际需要 f19~f20 等扩展字段，本次未拉）。
        // 因此 Bid1/Ask1 在东财源置 null，由对冲 fallback 到 Sina/Tencent 时填充。
        return new RealtimeQuote(
            Code: code,
            Name: name,
            Open: open,
            PreClose: preClose,
            Last: last,
            High: high,
            Low: low,
            Bid1: null,
            Ask1: null,
            Volume: volume,
            Amount: amount,
            Timestamp: ts,
            Source: "EastMoney");
    }

    private static JsonElement GetOrThrow(JsonElement data, string field, string code)
    {
        if (!data.TryGetProperty(field, out var v) || v.ValueKind == JsonValueKind.Null)
        {
            throw new DataSourceException("EastMoney", $"EastMoney data missing {field} for '{code}'.");
        }
        return v;
    }

    private static decimal NormalizePrice(JsonElement raw, decimal scale)
    {
        // 东财价格通常为整数 × 10^-f152；偶发出现浮点（如 f48），按场景安全转换。
        if (raw.ValueKind == JsonValueKind.Number)
        {
            if (raw.TryGetInt64(out var i))
            {
                return i / scale;
            }
            return (decimal)raw.GetDouble() / scale;
        }
        if (raw.ValueKind == JsonValueKind.String && decimal.TryParse(raw.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
        {
            return s / scale;
        }
        throw new DataSourceException("EastMoney", $"Unexpected price kind {raw.ValueKind}.");
    }

    private static string ResolveSecId(string anyForm) => EastMoneySecIdResolver.Resolve(anyForm);

    private static string ResolveResponseCode(string raw6, string requestedCode)
    {
        // 优先以「请求时的标准化代码」为准（保留显式市场前缀，避免对 BJ 等用 6 位推断错误）。
        if (CodeFormatter.TryParse(requestedCode, out var sc))
        {
            return sc.EastMoneyForm;
        }
        if (CodeFormatter.TryParse(raw6, out var fallback))
        {
            return fallback.EastMoneyForm;
        }
        return raw6;
    }
}
