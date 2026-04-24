using System.Globalization;
using System.Text;
using Baostock.NET.Http;
using Baostock.NET.Models;
using Baostock.NET.Util;

namespace Baostock.NET.Realtime;

/// <summary>
/// 腾讯实时行情数据源 (<c>http://qt.gtimg.cn/q=...</c>)。
/// </summary>
/// <remarks>
/// <para>响应：<c>v_sh600519="51~贵州茅台~600519~..."</c>，分隔符 <c>~</c>，编码 GBK / GB18030。</para>
/// <para>字段索引依据 <c>docs/v1.2.0-source-mapping.md</c> §2 表 1：
/// 1=名称、2=代码、3=现价、4=昨收、5=今开、6=成交量(手)、9=买一价、19=卖一价、
/// 30=时间戳 <c>yyyyMMddHHmmss</c>、33=最高、34=最低、37=成交额(<b>万元</b>)。</para>
/// <para>失败：单只 v_xxx="" 抛 <see cref="DataSourceException"/>(<c>code="EMPTY"</c>)。</para>
/// </remarks>
public sealed class TencentRealtimeSource : IDataSource<string[], IReadOnlyList<RealtimeQuote>>
{
    /// <summary>请求超时。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(8);

    private readonly HttpDataClient _http;

    /// <summary>构造一个腾讯实时行情数据源。</summary>
    /// <param name="http">HTTP 抓取客户端，缺省使用 <see cref="HttpDataClient.Default"/>。</param>
    public TencentRealtimeSource(HttpDataClient? http = null)
    {
        _http = http ?? HttpDataClient.Default;
    }

    /// <inheritdoc />
    public string Name => "Tencent";

    /// <inheritdoc />
    public int Priority => 1;

    /// <inheritdoc />
    public async Task<IReadOnlyList<RealtimeQuote>> FetchAsync(string[] request, CancellationToken cancellationToken)
    {
        if (request is null || request.Length == 0)
        {
            throw new ArgumentException("codes must not be empty.", nameof(request));
        }

        var tencentCodes = new string[request.Length];
        for (int i = 0; i < request.Length; i++)
        {
            tencentCodes[i] = CodeFormatter.Parse(request[i]).LowercaseNoDot;
        }
        var url = "http://qt.gtimg.cn/q=" + string.Join(',', tencentCodes);
        var body = await _http.GetStringAsync(url, encoding: Encoding.GetEncoding("GB18030"), timeout: Timeout, ct: cancellationToken).ConfigureAwait(false);
        return Parse(body, request);
    }

    internal static IReadOnlyList<RealtimeQuote> Parse(string body, string[] requestedCodes)
    {
        // 多行：每行 v_{code}="...";
        var quotes = new List<RealtimeQuote>(requestedCodes.Length);
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim().TrimEnd(';');
            if (line.Length == 0) continue;
            int eqIdx = line.IndexOf('=');
            if (eqIdx < 0 || eqIdx + 2 >= line.Length) continue;
            // v_sh600519="...."
            string varName = line[..eqIdx]; // v_sh600519
            string payload = line[(eqIdx + 1)..].Trim('"');
            if (payload.Length == 0)
            {
                var bareCode = varName.StartsWith("v_", StringComparison.Ordinal) ? varName[2..] : varName;
                throw new DataSourceException("Tencent", $"Tencent returned empty payload for '{bareCode}'.");
            }

            var fields = payload.Split('~');
            if (fields.Length < 35)
            {
                throw new DataSourceException("Tencent", $"Tencent payload has only {fields.Length} fields, expected ≥ 35.");
            }

            var name = fields[1];
            var rawCode = fields[2];
            var code = NormalizeCode(rawCode);
            var inv = CultureInfo.InvariantCulture;
            decimal last = decimal.Parse(fields[3], inv);
            decimal preClose = decimal.Parse(fields[4], inv);
            decimal open = decimal.Parse(fields[5], inv);
            // 索引 6 单位 = 手；统一为股 ×100
            long volLots = long.Parse(fields[6], inv);
            long volume = checked(volLots * 100L);
            // 索引 9/19 = 买一价/卖一价（腾讯顺序: price, qty, price, qty...）
            decimal? bid1 = TryParseDecimal(fields, 9, inv);
            decimal? ask1 = TryParseDecimal(fields, 19, inv);
            // 索引 30 = yyyyMMddHHmmss
            var ts = ParseTimestamp(fields[30]);
            decimal high = decimal.Parse(fields[33], inv);
            decimal low = decimal.Parse(fields[34], inv);
            // 索引 37 单位 = 万元；× 10000 → 元
            decimal amountWan = decimal.Parse(fields[37], inv);
            decimal amount = amountWan * 10000m;

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
                Source: "Tencent"));
        }

        if (quotes.Count != requestedCodes.Length)
        {
            throw new DataSourceException("Tencent",
                $"Tencent returned {quotes.Count} quotes, expected {requestedCodes.Length}.");
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

    private static DateTime ParseTimestamp(string s)
    {
        // yyyyMMddHHmmss
        if (s.Length != 14) return DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        return DateTime.SpecifyKind(
            DateTime.ParseExact(s, "yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            DateTimeKind.Unspecified);
    }

    private static string NormalizeCode(string raw6)
    {
        // 腾讯响应里的代码为纯 6 位数字，无市场前缀；按 6 位推断交易所。
        if (CodeFormatter.TryParse(raw6, out var sc))
        {
            return sc.EastMoneyForm;
        }
        return raw6;
    }
}
