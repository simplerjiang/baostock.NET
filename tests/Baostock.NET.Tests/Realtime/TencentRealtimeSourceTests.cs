using Baostock.NET.Http;
using Baostock.NET.Realtime;

namespace Baostock.NET.Tests.Realtime;

public class TencentRealtimeSourceTests
{
    // Sprint 0 实测落盘的真实响应（GBK 已解码）
    // 来自 tools/source-probes/raw/tencent_realtime_SH600519.txt
    private const string SH600519Body =
        "v_sh600519=\"1~贵州茅台~600519~1445.50~1419.00~1413.10~20862~11924~8921~1445.34~7~1445.01~4~1445.00~5~1444.80~1~1444.73~1~1445.60~3~1445.80~2~1445.86~1~1445.87~2~1445.88~36~~20260424102119~26.50~1.87~1450.00~1413.10~1445.50/20862/2991697134~20862~299170~0.17~21.99~~1450.00~1413.10~2.60~18101.57~18101.57~7.40~1560.90~1277.10~2.19~-26~1434.08~21.99~21.99~~~0.47~299169.7134~0.0000~0~ ~GP-A~4.96~2.72~3.57~33.65~28.08~1593.44~1322.01~-0.58~3.16~6.99~1252270215~1252270215~-41.94~1.01~1252270215~~~-3.68~0.09~~CNY~0~___D__F__N~1445.90~-7~\";";

    // tools/source-probes/raw/tencent_realtime_SZ000001.txt
    private const string SZ000001Body =
        "v_sz000001=\"51~平安银行~000001~11.03~11.00~10.98~239540~129039~110491~11.02~1633~11.01~2505~11.00~2970~10.99~2389~10.98~2537~11.03~7111~11.04~7801~11.05~9631~11.06~8675~11.07~4258~~20260424102118~0.03~0.27~11.03~10.92~11.03/239540/262696173~239540~26270~0.12~5.02~~11.03~10.92~1.00~2140.44~2140.47~0.47~12.10~9.90~1.52~-25442~10.97~5.02~5.02~~~0.46~26269.6173~0.0000~0~ ~GP-A~-3.33~0.18~5.42~7.73~0.72~13.09~10.29~-0.54~0.82~-0.36~19405600653~19405918198~-51.39~-8.95~19405600653~~~5.73~0.09~~CNY~0~~11.09~-12871~\";";

    [Fact]
    public void Parse_Single_Success()
    {
        var quotes = TencentRealtimeSource.Parse(SH600519Body, new[] { "SH600519" });

        Assert.Single(quotes);
        var q = quotes[0];
        Assert.Equal("SH600519", q.Code);
        Assert.Equal("贵州茅台", q.Name);
        Assert.Equal(1445.50m, q.Last);
        Assert.Equal(1419.00m, q.PreClose);
        Assert.Equal(1413.10m, q.Open);
        Assert.Equal(1450.00m, q.High);
        Assert.Equal(1413.10m, q.Low);
        Assert.Equal(1445.34m, q.Bid1);
        Assert.Equal(1445.60m, q.Ask1);
        // 腾讯 vol = 20862 手 → 2_086_200 股
        Assert.Equal(2_086_200L, q.Volume);
        // 索引 37 = 299170 万元 → 2_991_700_000 元
        Assert.Equal(2_991_700_000m, q.Amount);
        Assert.Equal("Tencent", q.Source);
        Assert.Equal(new DateTime(2026, 4, 24, 10, 21, 19), q.Timestamp);
        Assert.Equal(DateTimeKind.Unspecified, q.Timestamp.Kind);
    }

    [Fact]
    public void Parse_Batch_Success()
    {
        var body = SH600519Body + "\n" + SZ000001Body;
        var quotes = TencentRealtimeSource.Parse(body, new[] { "SH600519", "SZ000001" });

        Assert.Equal(2, quotes.Count);
        Assert.Equal("SH600519", quotes[0].Code);
        Assert.Equal("SZ000001", quotes[1].Code);
        Assert.Equal("平安银行", quotes[1].Name);
        Assert.Equal(11.03m, quotes[1].Last);
        Assert.Equal(239540L * 100L, quotes[1].Volume);
    }

    [Fact]
    public void Parse_EmptyPayload_Throws()
    {
        var body = "v_sh999999=\"\";";
        var ex = Assert.Throws<DataSourceException>(() =>
            TencentRealtimeSource.Parse(body, new[] { "SH999999" }));
        Assert.Equal("Tencent", ex.SourceName);
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
