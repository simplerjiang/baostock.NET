using System.Globalization;
using System.Text;
using Baostock.NET.Http;
using Baostock.NET.Models;
using Baostock.NET.Util;

namespace Baostock.NET.Realtime;

/// <summary>
/// 新浪实时行情数据源 (<c>http://hq.sinajs.cn/list=...</c>)。
/// </summary>
/// <remarks>
/// <para>响应：<c>var hq_str_sh600519="贵州茅台,1413.100,..."</c>，分隔符 <c>,</c>，编码 GBK / GB18030。</para>
/// <para>必带 Header <c>Referer: https://finance.sina.com.cn</c>，否则 403 / 空响应。</para>
/// <para>字段索引依据 <c>docs/v1.2.0-source-mapping.md</c> §2 表 1：
/// 0=名称、1=今开、2=昨收、3=现价、4=最高、5=最低、6=买一(委托)、7=卖一(委托)、
/// 8=成交量(<b>股</b>)、9=成交额(元)、10/11=买一档(qty,price)、20/21=卖一档(qty,price)、
/// 30=日期 <c>yyyy-MM-dd</c>、31=时间 <c>HH:mm:ss</c>。</para>
/// <para>失败：<c>hq_str_xxx=""</c> 抛 <see cref="DataSourceException"/>(<c>code="EMPTY"</c>)。</para>
/// </remarks>
public sealed class SinaRealtimeSource : IDataSource<string[], IReadOnlyList<RealtimeQuote>>
{
    /// <summary>Sina 行情接口必须的 Referer 头值。</summary>
    public const string RequiredReferer = "https://finance.sina.com.cn";

    /// <summary>请求超时。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(8);

    private readonly HttpDataClient _http;

    /// <summary>构造一个新浪实时行情数据源。</summary>
    /// <param name="http">HTTP 抓取客户端，缺省使用 <see cref="HttpDataClient.Default"/>。</param>
    public SinaRealtimeSource(HttpDataClient? http = null)
    {
        _http = http ?? HttpDataClient.Default;
    }

    /// <inheritdoc />
    public string Name => "Sina";

    /// <inheritdoc />
    public int Priority => 0;

    /// <inheritdoc />
    public async Task<IReadOnlyList<RealtimeQuote>> FetchAsync(string[] request, CancellationToken cancellationToken)
    {
        if (request is null || request.Length == 0)
        {
            throw new ArgumentException("codes must not be empty.", nameof(request));
        }

        var sinaCodes = new string[request.Length];
        for (int i = 0; i < request.Length; i++)
        {
            sinaCodes[i] = CodeFormatter.Parse(request[i]).LowercaseNoDot;
        }
        var url = "http://hq.sinajs.cn/list=" + string.Join(',', sinaCodes);
        var headers = new Dictionary<string, string> { ["Referer"] = RequiredReferer };
        var body = await _http.GetStringAsync(url, headers, Encoding.GetEncoding("GB18030"), Timeout, cancellationToken).ConfigureAwait(false);
        return Parse(body, request);
    }

    internal static IReadOnlyList<RealtimeQuote> Parse(string body, string[] requestedCodes)
    {
        var quotes = new List<RealtimeQuote>(requestedCodes.Length);
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim().TrimEnd(';');
            if (line.Length == 0) continue;
            // var hq_str_sh600519="..."
            int eqIdx = line.IndexOf('=');
            if (eqIdx < 0 || eqIdx + 2 >= line.Length) continue;
            string left = line[..eqIdx].Trim();
            string varName = left.StartsWith("var ", StringComparison.Ordinal) ? left[4..].Trim() : left;
            string bareCode = varName.StartsWith("hq_str_", StringComparison.Ordinal) ? varName[7..] : varName;
            string payload = line[(eqIdx + 1)..].Trim().Trim('"');
            if (payload.Length == 0)
            {
                throw new DataSourceException("Sina", $"Sina returned empty payload for '{bareCode}'.");
            }

            var fields = payload.Split(',');
            if (fields.Length < 32)
            {
                throw new DataSourceException("Sina", $"Sina payload has only {fields.Length} fields, expected ≥ 32.");
            }

            var inv = CultureInfo.InvariantCulture;
            string name = fields[0];
            decimal open = decimal.Parse(fields[1], inv);
            decimal preClose = decimal.Parse(fields[2], inv);
            decimal last = decimal.Parse(fields[3], inv);
            decimal high = decimal.Parse(fields[4], inv);
            decimal low = decimal.Parse(fields[5], inv);
            // Sina 对部分北交所 / 长期停牌股票返回 "name,0.000,0.000,...,0,0.000,..." —— payload 非空但全零；
            // 视为该源「实质为空」抛出 EMPTY，让 hedge fallback 到 Tencent / EastMoney（它们能返回 PreClose）。
            if (preClose == 0m && open == 0m && last == 0m && high == 0m && low == 0m)
            {
                throw new DataSourceException("Sina",
                    $"Sina returned all-zero payload for '{bareCode}' (likely halted / pre-market for BJ).");
            }
            // Sina 五档买盘从 idx=10 起（qty,price 顺序），故买一价是 fields[11]
            decimal? bid1 = TryParseDecimal(fields, 11, inv);
            // Sina 五档卖盘从 idx=20 起（qty,price 顺序），故卖一价是 fields[21]
            decimal? ask1 = TryParseDecimal(fields, 21, inv);
            // 索引 8 = 成交量（股，原值）
            long volume = (long)decimal.Parse(fields[8], inv);
            // 索引 9 = 成交额（元，3 位小数字符串）
            decimal amount = decimal.Parse(fields[9], inv);
            // 索引 30 = yyyy-MM-dd, 31 = HH:mm:ss
            var ts = ParseTimestamp(fields[30], fields[31]);
            string code = ResolveCode(bareCode);

            quotes.Add(new RealtimeQuote(
                Code: code,
                Name: name,
                Open: open,
                PreClose: preClose,
                Last: last,
                High: high,
                Low: low,
                Bid1: bid1,
                Ask1: ask1,
                Volume: volume,
                Amount: amount,
                Timestamp: ts,
                Source: "Sina"));
        }

        if (quotes.Count != requestedCodes.Length)
        {
            throw new DataSourceException("Sina",
                $"Sina returned {quotes.Count} quotes, expected {requestedCodes.Length}.");
        }
        return quotes;
    }

    private static decimal? TryParseDecimal(string[] fields, int idx, IFormatProvider inv)
    {
        if (idx < 0 || idx >= fields.Length) return null;
        var s = fields[idx];
        if (string.IsNullOrWhiteSpace(s)) return null;
        return decimal.TryParse(s, NumberStyles.Float, inv, out var v) ? v : null;
    }

    private static DateTime ParseTimestamp(string date, string time)
    {
        if (string.IsNullOrWhiteSpace(date) || string.IsNullOrWhiteSpace(time))
        {
            return DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        }
        var s = date + " " + time;
        return DateTime.SpecifyKind(
            DateTime.ParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeKind.Unspecified);
    }

    private static string ResolveCode(string bareLowerWithPrefix)
    {
        // bareLowerWithPrefix 形如 sh600519 / sz000001 / bj430047
        if (CodeFormatter.TryParse(bareLowerWithPrefix, out var sc))
        {
            return sc.EastMoneyForm;
        }
        return bareLowerWithPrefix;
    }
}
