using Baostock.NET.Http;
using Baostock.NET.KLine;
using Baostock.NET.Models;

namespace Baostock.NET.Tests.KLine;

public class TencentKLineSourceTests
{
    // ───────── Fixtures（来自 tools/source-probes/raw/，每行实测） ─────────

    // tools/source-probes/raw/tencent_kline_day_qfq_SH600519.txt 头 3 行
    // 顺序陷阱样本：2026-03-13 → open=1392.48, close=1413.64, high=1417.62, low=1392.00
    //                               ^^^^^^^^^ close (1413.64) < high (1417.62)
    private const string SH600519_Day_Qfq_Json =
        "{\"code\":0,\"msg\":\"\",\"data\":{\"sh600519\":{\"qfqday\":[" +
        "[\"2026-03-12\",\"1395.000\",\"1392.000\",\"1403.950\",\"1391.010\",\"27586.000\"]," +
        "[\"2026-03-13\",\"1392.480\",\"1413.640\",\"1417.620\",\"1392.000\",\"33608.000\"]," +
        "[\"2026-03-16\",\"1420.000\",\"1460.180\",\"1466.000\",\"1420.000\",\"60086.000\"]" +
        "]}}}";

    // tools/source-probes/raw/tencent_kline_5m_SH600519.txt 头 2 行（保留腾讯第 7={}, 第 8="0.xx" 占位）
    private const string SH600519_5m_Json =
        "{\"code\":0,\"data\":{\"sh600519\":{\"m5\":[" +
        "[\"202604231330\",\"1415.71\",\"1416.41\",\"1417.54\",\"1415.70\",\"279.00\",{},\"0.2228\"]," +
        "[\"202604231335\",\"1416.46\",\"1417.40\",\"1418.43\",\"1416.41\",\"459.00\",{},\"0.3665\"]" +
        "]}}}";

    // tools/source-probes/raw/tencent_kline_day_qfq_SZ000001.txt 头 2 行
    private const string SZ000001_Day_Qfq_Json =
        "{\"code\":0,\"data\":{\"sz000001\":{\"qfqday\":[" +
        "[\"2026-03-12\",\"10.870\",\"10.940\",\"10.960\",\"10.850\",\"754906.000\"]," +
        "[\"2026-03-13\",\"10.930\",\"10.930\",\"11.000\",\"10.870\",\"841939.000\"]" +
        "]}}}";

    [Fact]
    public void Parse_Daily_SH600519_Qfq_Success()
    {
        var rows = TencentKLineSource.Parse(
            SH600519_Day_Qfq_Json, emCode: "SH600519", tencentCode: "sh600519",
            period: "day", fq: "qfq", isIntraday: false);
        Assert.Equal(3, rows.Count);

        var r0 = rows[0];
        Assert.Equal("SH600519", r0.Code);
        Assert.Equal(new DateTime(2026, 3, 12), r0.Date);
        Assert.Equal(1395.000m, r0.Open);
        Assert.Equal(1392.000m, r0.Close);
        Assert.Equal(1403.950m, r0.High);
        Assert.Equal(1391.010m, r0.Low);
        // 27586 手 → 2_758_600 股
        Assert.Equal(2_758_600L, r0.Volume);
        // 腾讯不返回这些字段
        Assert.Null(r0.Amount);
        Assert.Null(r0.Amplitude);
        Assert.Null(r0.ChangePercent);
        Assert.Equal("Tencent", r0.Source);
    }

    [Fact]
    public void Parse_Daily_FieldOrderTrap_CloseBeforeHigh()
    {
        // 腾讯字段顺序陷阱专项：每行是 [date,open,close,high,low,vol]，第 3 个是 close 不是 high。
        // 真实 fixture 行：["2026-03-13","1392.480","1413.640","1417.620","1392.000","33608.000"]
        // 若错误按 OHLC 顺序解析，会得到 high=1413.64 (实为 close)。
        var rows = TencentKLineSource.Parse(
            SH600519_Day_Qfq_Json, emCode: "SH600519", tencentCode: "sh600519",
            period: "day", fq: "qfq", isIntraday: false);
        var trapRow = rows[1]; // 2026-03-13
        Assert.Equal(new DateTime(2026, 3, 13), trapRow.Date);
        Assert.Equal(1392.48m, trapRow.Open);
        Assert.Equal(1413.64m, trapRow.Close);
        Assert.Equal(1417.62m, trapRow.High);
        // 关键断言：close 严格小于 high，证明字段不是按 OHLC 错位解析的。
        Assert.True(trapRow.Close < trapRow.High,
            $"Tencent field order trap: close ({trapRow.Close}) must be < high ({trapRow.High}).");
        // 反向校验：若错误按 OHLC 排序，high 会是 1413.64 而 low 是 1417.62（不可能）。
        Assert.True(trapRow.High > trapRow.Open);
        Assert.True(trapRow.High > trapRow.Close);
        Assert.True(trapRow.Low < trapRow.Open);
        Assert.True(trapRow.Low < trapRow.Close);
    }

    [Fact]
    public void Parse_FiveMinute_SH600519_Success()
    {
        var rows = TencentKLineSource.Parse(
            SH600519_5m_Json, emCode: "SH600519", tencentCode: "sh600519",
            period: "m5", fq: string.Empty, isIntraday: true);
        Assert.Equal(2, rows.Count);
        var r0 = rows[0];
        // "202604231330" → 2026-04-23 13:30
        Assert.Equal(new DateTime(2026, 4, 23, 13, 30, 0), r0.Date);
        Assert.Equal(1415.71m, r0.Open);
        Assert.Equal(1416.41m, r0.Close);
        Assert.Equal(1417.54m, r0.High);
        Assert.Equal(1415.70m, r0.Low);
        Assert.Equal(27_900L, r0.Volume);
    }

    [Fact]
    public void Parse_Daily_SZ000001_Qfq_Success()
    {
        var rows = TencentKLineSource.Parse(
            SZ000001_Day_Qfq_Json, emCode: "SZ000001", tencentCode: "sz000001",
            period: "day", fq: "qfq", isIntraday: false);
        Assert.Equal(2, rows.Count);
        Assert.Equal("SZ000001", rows[0].Code);
        Assert.Equal(10.870m, rows[0].Open);
        Assert.Equal(10.940m, rows[0].Close);
    }

    [Fact]
    public void Parse_MissingCodeKey_Throws()
    {
        // data 下没有 sh600519 这个 key
        var json = "{\"code\":0,\"data\":{\"sh999999\":{\"qfqday\":[]}}}";
        var ex = Assert.Throws<DataSourceException>(() =>
            TencentKLineSource.Parse(json, "SH600519", "sh600519", "day", "qfq", isIntraday: false));
        Assert.Equal("Tencent", ex.SourceName);
        Assert.Contains("missing key", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_NoKlineArray_Throws()
    {
        // 有 data.sh600519 但没有 qfqday/day key
        var json = "{\"code\":0,\"data\":{\"sh600519\":{\"qt\":{}}}}";
        var ex = Assert.Throws<DataSourceException>(() =>
            TencentKLineSource.Parse(json, "SH600519", "sh600519", "day", "qfq", isIntraday: false));
        Assert.Contains("no kline array", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
