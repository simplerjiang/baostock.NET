using Baostock.NET.Http;
using Baostock.NET.KLine;
using Baostock.NET.Models;

namespace Baostock.NET.Tests.KLine;

public class EastMoneyKLineSourceTests
{
    // ───────────────── Fixtures（从 tools/source-probes/raw/ 取真实片段） ─────────────────

    // tools/source-probes/raw/eastmoney_kline_day_SH600519.txt 头 3 行
    // 字段：date,open,close,high,low,vol(手),amount(元),振幅%,涨跌幅%,涨跌额,换手率%
    private const string SH600519_Day_Json =
        "{\"rc\":0,\"data\":{\"code\":\"600519\",\"market\":1,\"name\":\"贵州茅台\",\"decimal\":2," +
        "\"klines\":[" +
        "\"2026-03-13,1392.48,1413.64,1417.62,1392.00,33608,4738808558.00,1.84,1.55,21.64,0.27\"," +
        "\"2026-03-16,1420.00,1460.18,1466.00,1420.00,60086,8711525836.00,3.25,3.29,46.54,0.48\"," +
        "\"2026-03-17,1468.00,1485.00,1498.07,1461.19,49454,7347310632.00,2.53,1.70,24.82,0.39\"" +
        "]}}";

    // tools/source-probes/raw/eastmoney_kline_5m_SH600519.txt 头 2 行
    private const string SH600519_5m_Json =
        "{\"rc\":0,\"data\":{\"code\":\"600519\",\"market\":1,\"name\":\"贵州茅台\"," +
        "\"klines\":[" +
        "\"2026-04-23 13:30,1415.71,1416.41,1417.54,1415.70,279,39510718.00,0.13,0.05,0.73,0.00\"," +
        "\"2026-04-23 13:35,1416.46,1417.40,1418.43,1416.41,459,65075484.00,0.14,0.07,0.99,0.00\"" +
        "]}}";

    // tools/source-probes/raw/eastmoney_kline_day_SZ000001.txt 头 2 行
    private const string SZ000001_Day_Json =
        "{\"rc\":0,\"data\":{\"code\":\"000001\",\"market\":0,\"name\":\"平安银行\"," +
        "\"klines\":[" +
        "\"2026-03-13,10.93,10.93,11.00,10.87,841939,920839355.51,1.19,-0.09,-0.01,0.43\"," +
        "\"2026-03-16,10.93,10.92,10.97,10.88,715603,782089440.63,0.82,-0.09,-0.01,0.37\"" +
        "]}}";

    [Fact]
    public void Parse_Daily_SH600519_Success()
    {
        var rows = EastMoneyKLineSource.Parse(SH600519_Day_Json, "SH600519", isIntraday: false);
        Assert.Equal(3, rows.Count);

        var r0 = rows[0];
        Assert.Equal("SH600519", r0.Code);
        Assert.Equal(new DateTime(2026, 3, 13), r0.Date);
        Assert.Equal(1392.48m, r0.Open);
        Assert.Equal(1413.64m, r0.Close);
        Assert.Equal(1417.62m, r0.High);
        Assert.Equal(1392.00m, r0.Low);
        // 33608 手 → 3_360_800 股
        Assert.Equal(3_360_800L, r0.Volume);
        Assert.Equal(4738808558.00m, r0.Amount);
        Assert.Equal(1.84m, r0.Amplitude);
        Assert.Equal(1.55m, r0.ChangePercent);
        Assert.Equal(21.64m, r0.ChangeAmount);
        Assert.Equal(0.27m, r0.TurnoverRate);
        Assert.Equal("EastMoney", r0.Source);
    }

    [Fact]
    public void Parse_FiveMinute_SH600519_Success()
    {
        var rows = EastMoneyKLineSource.Parse(SH600519_5m_Json, "SH600519", isIntraday: true);
        Assert.Equal(2, rows.Count);
        var r0 = rows[0];
        // 分钟戳：2026-04-23 13:30
        Assert.Equal(new DateTime(2026, 4, 23, 13, 30, 0), r0.Date);
        Assert.Equal(1415.71m, r0.Open);
        Assert.Equal(1416.41m, r0.Close);
        Assert.Equal(1417.54m, r0.High);
        Assert.Equal(1415.70m, r0.Low);
        // 279 手 → 27_900 股
        Assert.Equal(27_900L, r0.Volume);
    }

    [Fact]
    public void Parse_Daily_SZ000001_Success()
    {
        var rows = EastMoneyKLineSource.Parse(SZ000001_Day_Json, "SZ000001", isIntraday: false);
        Assert.Equal(2, rows.Count);
        var r0 = rows[0];
        Assert.Equal("SZ000001", r0.Code);
        Assert.Equal(10.93m, r0.Open);
        Assert.Equal(10.93m, r0.Close);
        Assert.Equal(11.00m, r0.High);
        Assert.Equal(10.87m, r0.Low);
        Assert.Equal(841939L * 100L, r0.Volume);
    }

    [Fact]
    public void Parse_NullData_Throws()
    {
        var ex1 = Assert.Throws<DataSourceException>(() =>
            EastMoneyKLineSource.Parse("{\"rc\":102,\"data\":null}", "SH999999", isIntraday: false));
        Assert.Equal("EastMoney", ex1.SourceName);
        Assert.Contains("no data", ex1.Message, StringComparison.OrdinalIgnoreCase);

        var ex2 = Assert.Throws<DataSourceException>(() =>
            EastMoneyKLineSource.Parse("{\"rc\":0,\"data\":{\"klines\":[]}}", "SH600519", isIntraday: false));
        Assert.Contains("empty klines", ex2.Message, StringComparison.OrdinalIgnoreCase);
    }
}
