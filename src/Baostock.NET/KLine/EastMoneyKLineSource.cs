using System.Globalization;
using System.Text.Json;
using Baostock.NET.Http;
using Baostock.NET.Models;
using Baostock.NET.Util;

namespace Baostock.NET.KLine;

/// <summary>
/// 东方财富历史 K 线数据源 (<c>https://push2his.eastmoney.com/api/qt/stock/kline/get</c>)。
/// </summary>
/// <remarks>
/// <para>响应：<c>{"data":{"klines":["2026-04-23,1408.00,1419.00,1419.70,1405.10,37701,5332831151.00,1.04,0.67,9.50,0.30",...]}}</c>。</para>
/// <para>每行 11 个逗号分隔字段：<b>date,open,close,high,low,volume(手),amount(元),振幅%,涨跌幅%,涨跌额,换手率%</b>
/// （详见 <c>docs/v1.2.0-source-mapping.md</c> §2 表 2~3）。注意 <b>open 之后是 close 不是 high</b>。</para>
/// <para>分钟线 date 形如 <c>"2026-04-23 13:30"</c>（带空格），日/周/月线为 <c>"2026-04-23"</c>。</para>
/// <para>失败：<c>data == null</c> 或 <c>klines</c> 为空抛 <see cref="DataSourceException"/>(<c>"EMPTY"</c>)。</para>
/// <para>Header：必带 <c>Referer: https://quote.eastmoney.com/</c> + <c>ut</c> token，否则易 502。</para>
/// </remarks>
public sealed class EastMoneyKLineSource : IDataSource<KLineRequest, IReadOnlyList<EastMoneyKLineRow>>
{
    /// <summary>请求超时。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>东财 ut token，用于减小 502 概率。</summary>
    public const string DefaultUt = "fa5fd1943c7b386f172d6893dbfba10b";

    /// <summary>东财 K 线接口必须的 Referer。</summary>
    public const string RequiredReferer = "https://quote.eastmoney.com/";

    private readonly HttpDataClient _http;

    /// <summary>构造一个东方财富 K 线数据源。</summary>
    /// <param name="http">HTTP 抓取客户端，缺省使用 <see cref="HttpDataClient.Default"/>。</param>
    public EastMoneyKLineSource(HttpDataClient? http = null)
    {
        _http = http ?? HttpDataClient.Default;
    }

    /// <inheritdoc />
    public string Name => "EastMoney";

    /// <inheritdoc />
    public int Priority => 0;

    /// <inheritdoc />
    public async Task<IReadOnlyList<EastMoneyKLineRow>> FetchAsync(KLineRequest request, CancellationToken cancellationToken)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        var sc = CodeFormatter.Parse(request.Code);
        var secid = EastMoneySecIdResolver.Resolve(sc);
        var klt = KLineFrequencyMapping.ToEastMoneyKlt(request.Frequency);
        var fqt = KLineFrequencyMapping.ToEastMoneyFqt(request.Adjust);
        var beg = request.StartDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var end = request.EndDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var url = "https://push2his.eastmoney.com/api/qt/stock/kline/get"
            + "?secid=" + secid
            + "&ut=" + DefaultUt
            + "&klt=" + klt
            + "&fqt=" + fqt
            + "&beg=" + beg
            + "&end=" + end
            + "&lmt=1000000"
            + "&fields1=f1,f2,f3,f4,f5,f6"
            + "&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61";
        var headers = new Dictionary<string, string> { ["Referer"] = RequiredReferer };
        var json = await _http.GetStringAsync(url, headers, encoding: null, timeout: Timeout, ct: cancellationToken).ConfigureAwait(false);
        return Parse(json, sc.EastMoneyForm, KLineFrequencyMapping.IsIntraday(request.Frequency));
    }

    internal static IReadOnlyList<EastMoneyKLineRow> Parse(string json, string requestedCode, bool isIntraday)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            throw new DataSourceException("EastMoney", $"EastMoney returned no data for '{requestedCode}'.");
        }
        if (!data.TryGetProperty("klines", out var klines) || klines.ValueKind != JsonValueKind.Array || klines.GetArrayLength() == 0)
        {
            throw new DataSourceException("EastMoney", $"EastMoney returned empty klines for '{requestedCode}'.");
        }

        var inv = CultureInfo.InvariantCulture;
        var rows = new List<EastMoneyKLineRow>(klines.GetArrayLength());
        foreach (var line in klines.EnumerateArray())
        {
            if (line.ValueKind != JsonValueKind.String) continue;
            var raw = line.GetString();
            if (string.IsNullOrEmpty(raw)) continue;
            var f = raw.Split(',');
            if (f.Length < 6)
            {
                throw new DataSourceException("EastMoney", $"EastMoney kline row has only {f.Length} fields, expected ≥ 6: '{raw}'.");
            }
            var date = ParseDate(f[0], isIntraday);
            decimal open = decimal.Parse(f[1], NumberStyles.Float, inv);
            decimal close = decimal.Parse(f[2], NumberStyles.Float, inv);
            decimal high = decimal.Parse(f[3], NumberStyles.Float, inv);
            decimal low = decimal.Parse(f[4], NumberStyles.Float, inv);
            // f[5] = 成交量（手） → ×100 归一化为股
            long volLots = long.Parse(f[5], NumberStyles.Float, inv);
            long volume = checked(volLots * 100L);
            decimal? amount = TryParseDecimal(f, 6, inv);
            decimal? amplitude = TryParseDecimal(f, 7, inv);
            decimal? changePct = TryParseDecimal(f, 8, inv);
            decimal? changeAmt = TryParseDecimal(f, 9, inv);
            decimal? turnover = TryParseDecimal(f, 10, inv);

            rows.Add(new EastMoneyKLineRow(
                Code: requestedCode,
                Date: date,
                Open: open,
                Close: close,
                High: high,
                Low: low,
                Volume: volume,
                Amount: amount,
                Amplitude: amplitude,
                ChangePercent: changePct,
                ChangeAmount: changeAmt,
                TurnoverRate: turnover,
                Source: "EastMoney"));
        }
        return rows;
    }

    private static DateTime ParseDate(string s, bool isIntraday)
    {
        // 日/周/月: "2026-04-23"; 分钟: "2026-04-23 13:30"
        if (isIntraday)
        {
            return DateTime.SpecifyKind(
                DateTime.ParseExact(s, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                DateTimeKind.Unspecified);
        }
        return DateTime.SpecifyKind(
            DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeKind.Unspecified);
    }

    private static decimal? TryParseDecimal(string[] fields, int idx, IFormatProvider inv)
    {
        if (idx < 0 || idx >= fields.Length) return null;
        var s = fields[idx];
        if (string.IsNullOrWhiteSpace(s)) return null;
        return decimal.TryParse(s, NumberStyles.Float, inv, out var v) ? v : null;
    }
}
