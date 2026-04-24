using System.Globalization;
using System.Text.Json;
using Baostock.NET.Http;
using Baostock.NET.Models;
using Baostock.NET.Util;

namespace Baostock.NET.KLine;

/// <summary>
/// 腾讯历史 K 线数据源（备用源，字段比东财少 5 列）。
/// </summary>
/// <remarks>
/// <para>端点：</para>
/// <list type="bullet">
///   <item><description>日/周/月 + 前/后复权：<c>https://web.ifzq.gtimg.cn/appstock/app/fqkline/get?param={code},{period},{start},{end},{count},{fq}</c>，容器 key = <c>qfq{period}</c> / <c>hfq{period}</c></description></item>
///   <item><description>日/周/月 + 不复权：<c>https://web.ifzq.gtimg.cn/appstock/app/kline/kline?param={code},{period},{start},{end},{count},</c>，容器 key = <c>{period}</c></description></item>
///   <item><description>分钟（5/15/30/60，<b>仅原始数据，不支持复权</b>）：<c>https://web.ifzq.gtimg.cn/appstock/app/kline/mkline?param={code},{period},,{count}</c>，容器 key = <c>m5</c> / <c>m15</c> / ...</description></item>
/// </list>
/// <para><b>字段顺序陷阱</b>：每行数组顺序是 <b><c>[date, open, close, high, low, volume]</c></b>（详见 <c>docs/v1.2.0-source-mapping.md</c> §2 表 2~3，
/// SH600519 2026-03-13 实测 <c>open=1392.48 / close=1413.64 / high=1417.62</c>，<b>close &lt; high</b>），
/// 不是常见的 OHLC 顺序——禁止凭印象写成 high 在 close 之前。</para>
/// <para>分钟线第 7 元素是空字典占位 <c>{}</c>、第 8 元素是某个比率字符串，本实现按前 6 元素解析。</para>
/// <para>日期格式：日/周/月 = <c>"2026-04-23"</c>；分钟 = <c>"202604231330"</c>（无分隔的 yyyyMMddHHmm）。</para>
/// <para>成交量原始单位 = 手（字符串 "33608.000"），SDK ×100 归一化为股。</para>
/// <para>失败：响应 JSON 内未找到 <c>data.{code}</c> 或对应容器 key 不存在 / 数组为空 → 抛 <see cref="DataSourceException"/>(<c>"EMPTY"</c>)。</para>
/// </remarks>
public sealed class TencentKLineSource : IDataSource<KLineRequest, IReadOnlyList<EastMoneyKLineRow>>
{
    /// <summary>请求超时。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    private readonly HttpDataClient _http;

    /// <summary>构造一个腾讯 K 线数据源。</summary>
    /// <param name="http">HTTP 抓取客户端，缺省使用 <see cref="HttpDataClient.Default"/>。</param>
    public TencentKLineSource(HttpDataClient? http = null)
    {
        _http = http ?? HttpDataClient.Default;
    }

    /// <inheritdoc />
    public string Name => "Tencent";

    /// <inheritdoc />
    public int Priority => 1;

    /// <inheritdoc />
    public async Task<IReadOnlyList<EastMoneyKLineRow>> FetchAsync(KLineRequest request, CancellationToken cancellationToken)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        var sc = CodeFormatter.Parse(request.Code);
        var tencentCode = sc.LowercaseNoDot;
        var period = KLineFrequencyMapping.ToTencentPeriod(request.Frequency);
        var fq = KLineFrequencyMapping.ToTencentFq(request.Adjust);
        var isIntraday = KLineFrequencyMapping.IsIntraday(request.Frequency);

        // 构造 URL
        string url;
        if (isIntraday)
        {
            // mkline 不接受日期范围；按经验最大约 320 根。复权对分钟无效。
            url = "https://web.ifzq.gtimg.cn/appstock/app/kline/mkline"
                + "?param=" + tencentCode + "," + period + ",,320";
        }
        else
        {
            var start = request.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var end = request.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            const int Count = 800; // 单次拉取上限（实测约 800）
            if (fq.Length == 0)
            {
                url = "https://web.ifzq.gtimg.cn/appstock/app/kline/kline"
                    + "?param=" + tencentCode + "," + period
                    + "," + start + "," + end + "," + Count + ",";
            }
            else
            {
                url = "https://web.ifzq.gtimg.cn/appstock/app/fqkline/get"
                    + "?param=" + tencentCode + "," + period
                    + "," + start + "," + end + "," + Count + "," + fq;
            }
        }

        var json = await _http.GetStringAsync(url, headers: null, encoding: null, timeout: Timeout, ct: cancellationToken).ConfigureAwait(false);
        return Parse(json, sc.EastMoneyForm, tencentCode, period, fq, isIntraday);
    }

    internal static IReadOnlyList<EastMoneyKLineRow> Parse(
        string json,
        string emCode,
        string tencentCode,
        string period,
        string fq,
        bool isIntraday)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            throw new DataSourceException("Tencent", $"Tencent returned no data for '{tencentCode}'.");
        }
        if (!data.TryGetProperty(tencentCode, out var stockObj) || stockObj.ValueKind != JsonValueKind.Object)
        {
            throw new DataSourceException("Tencent", $"Tencent response missing key 'data.{tencentCode}'.");
        }

        // 容器 key 候选顺序（按实际请求形态优先匹配 → 兜底）：
        // - 分钟：先 {period}（如 m5），再 qfq{period}
        // - 日/周/月：按 fq 决定首选 key，未命中再回退 {period}
        var candidateKeys = new List<string>(3);
        if (isIntraday)
        {
            candidateKeys.Add(period);
            candidateKeys.Add("qfq" + period);
        }
        else if (fq == "qfq")
        {
            candidateKeys.Add("qfq" + period);
            candidateKeys.Add(period);
        }
        else if (fq == "hfq")
        {
            candidateKeys.Add("hfq" + period);
            candidateKeys.Add(period);
        }
        else
        {
            candidateKeys.Add(period);
        }

        JsonElement? klArr = null;
        foreach (var k in candidateKeys)
        {
            if (stockObj.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Array && v.GetArrayLength() > 0)
            {
                klArr = v;
                break;
            }
        }
        if (klArr is null)
        {
            throw new DataSourceException("Tencent",
                $"Tencent response has no kline array under any of [{string.Join(",", candidateKeys)}] for '{tencentCode}'.");
        }

        var inv = CultureInfo.InvariantCulture;
        var rows = new List<EastMoneyKLineRow>(klArr.Value.GetArrayLength());
        foreach (var elem in klArr.Value.EnumerateArray())
        {
            if (elem.ValueKind != JsonValueKind.Array || elem.GetArrayLength() < 6)
            {
                throw new DataSourceException("Tencent",
                    $"Tencent kline row malformed (kind={elem.ValueKind}, len={(elem.ValueKind == JsonValueKind.Array ? elem.GetArrayLength() : 0)}).");
            }
            // ⚠ 字段顺序：[date, open, close, high, low, volume]，第 3 个是 close 不是 high。
            var dateStr = elem[0].GetString() ?? string.Empty;
            var openStr = elem[1].GetString() ?? string.Empty;
            var closeStr = elem[2].GetString() ?? string.Empty;
            var highStr = elem[3].GetString() ?? string.Empty;
            var lowStr = elem[4].GetString() ?? string.Empty;
            var volStr = elem[5].GetString() ?? string.Empty;

            var date = ParseDate(dateStr, isIntraday);
            decimal open = decimal.Parse(openStr, NumberStyles.Float, inv);
            decimal close = decimal.Parse(closeStr, NumberStyles.Float, inv);
            decimal high = decimal.Parse(highStr, NumberStyles.Float, inv);
            decimal low = decimal.Parse(lowStr, NumberStyles.Float, inv);
            // 腾讯 vol 为带小数的「手」字符串，先转 decimal 再 ×100 → long
            decimal volLots = decimal.Parse(volStr, NumberStyles.Float, inv);
            long volume = (long)(volLots * 100m);

            rows.Add(new EastMoneyKLineRow(
                Code: emCode,
                Date: date,
                Open: open,
                Close: close,
                High: high,
                Low: low,
                Volume: volume,
                Amount: null,
                Amplitude: null,
                ChangePercent: null,
                ChangeAmount: null,
                TurnoverRate: null,
                Source: "Tencent"));
        }
        return rows;
    }

    private static DateTime ParseDate(string s, bool isIntraday)
    {
        if (isIntraday)
        {
            // 分钟："202604231330" (yyyyMMddHHmm, 12 位)
            return DateTime.SpecifyKind(
                DateTime.ParseExact(s, "yyyyMMddHHmm", CultureInfo.InvariantCulture),
                DateTimeKind.Unspecified);
        }
        // 日/周/月："2026-04-23"
        return DateTime.SpecifyKind(
            DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeKind.Unspecified);
    }
}
