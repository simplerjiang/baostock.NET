using Baostock.NET.Http;
using Baostock.NET.Realtime;

namespace Baostock.NET.Tests.Realtime;

public class SinaRealtimeSourceTests
{
    // tools/source-probes/raw/sina_realtime_SH600519.txt
    private const string SH600519Body =
        "var hq_str_sh600519=\"贵州茅台,1413.100,1419.000,1445.500,1450.000,1413.100,1445.340,1445.600,2086151,2991697134.000,700,1445.340,400,1445.010,500,1445.000,100,1444.800,100,1444.730,300,1445.600,200,1445.800,100,1445.860,200,1445.870,3600,1445.880,2026-04-24,10:21:19,00,\";";

    // tools/source-probes/raw/sina_realtime_SZ000001.txt
    private const string SZ000001Body =
        "var hq_str_sz000001=\"平安银行,10.980,11.000,11.020,11.030,10.920,11.020,11.030,23958799,262749104.760,163891,11.020,250500,11.010,297500,11.000,238900,10.990,253700,10.980,710076,11.030,779105,11.040,953115,11.050,867500,11.060,425800,11.070,2026-04-24,10:21:21,00\";";

    [Fact]
    public void Parse_Single_Success()
    {
        var quotes = SinaRealtimeSource.Parse(SH600519Body, new[] { "SH600519" });

        Assert.Single(quotes);
        var q = quotes[0];
        Assert.Equal("SH600519", q.Code);
        Assert.Equal("贵州茅台", q.Name);
        Assert.Equal(1413.100m, q.Open);
        Assert.Equal(1419.000m, q.PreClose);
        Assert.Equal(1445.500m, q.Last);
        Assert.Equal(1450.000m, q.High);
        Assert.Equal(1413.100m, q.Low);
        // 五档买盘起始 idx=10（qty,price 顺序），买一价在 idx=11
        Assert.Equal(1445.340m, q.Bid1);
        // 五档卖盘起始 idx=20（qty,price 顺序），卖一价在 idx=21
        Assert.Equal(1445.600m, q.Ask1);
        // Sina vol 单位即「股」原值
        Assert.Equal(2_086_151L, q.Volume);
        Assert.Equal(2_991_697_134.000m, q.Amount);
        Assert.Equal(new DateTime(2026, 4, 24, 10, 21, 19), q.Timestamp);
        Assert.Equal("Sina", q.Source);
    }

    [Fact]
    public void Parse_Batch_Success()
    {
        var body = SH600519Body + "\n" + SZ000001Body;
        var quotes = SinaRealtimeSource.Parse(body, new[] { "SH600519", "SZ000001" });

        Assert.Equal(2, quotes.Count);
        Assert.Equal("SZ000001", quotes[1].Code);
        Assert.Equal(11.020m, quotes[1].Last);
        Assert.Equal(23_958_799L, quotes[1].Volume);
    }

    [Fact]
    public void Parse_EmptyPayload_Throws()
    {
        var body = "var hq_str_sh999999=\"\";";
        var ex = Assert.Throws<DataSourceException>(() =>
            SinaRealtimeSource.Parse(body, new[] { "SH999999" }));
        Assert.Equal("Sina", ex.SourceName);
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
