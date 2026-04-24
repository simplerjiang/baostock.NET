using Baostock.NET.Http;
using Baostock.NET.Realtime;

namespace Baostock.NET.Tests.Realtime;

public class EastMoneyRealtimeSourceTests
{
    // tools/source-probes/raw/eastmoney_realtime_SH600519.txt
    private const string SH600519Json =
        "{\"rc\":0,\"rt\":4,\"svr\":177542533,\"lt\":1,\"full\":1,\"dlmkts\":\"8,10,128\",\"data\":{\"f43\":144541,\"f44\":145000,\"f45\":141310,\"f46\":141310,\"f47\":21238,\"f48\":3046049198.0,\"f49\":12085,\"f50\":215,\"f51\":156090,\"f52\":127710,\"f57\":\"600519\",\"f58\":\"贵州茅台\",\"f60\":141900,\"f86\":1776997417,\"f116\":1810043891463.1501,\"f117\":1810043891463.1501,\"f152\":2,\"f162\":2199,\"f163\":2199,\"f164\":2199,\"f167\":740,\"f168\":17,\"f169\":2641,\"f170\":186,\"f171\":260,\"f191\":-7037,\"f192\":-38}}";

    // tools/source-probes/raw/eastmoney_realtime_SZ000001.txt （含重复 key，System.Text.Json 取最后一次）
    private const string SZ000001Json =
        "{\"rc\":0,\"rt\":4,\"svr\":183642220,\"lt\":1,\"full\":1,\"dlmkts\":\"\",\"data\":{\"f43\":1101,\"f43\":1101,\"f44\":1103,\"f44\":1103,\"f45\":1092,\"f45\":1092,\"f46\":1098,\"f46\":1098,\"f47\":241850,\"f47\":241850,\"f48\":265241431.58,\"f48\":265241431.58,\"f49\":129474,\"f50\":150,\"f51\":1210,\"f51\":1210,\"f52\":990,\"f52\":990,\"f57\":\"000001\",\"f58\":\"平安银行\",\"f60\":1100,\"f60\":1100,\"f86\":1776997344,\"f116\":213659159359.98,\"f117\":213655663189.53,\"f152\":2,\"f162\":501,\"f163\":501,\"f164\":501,\"f167\":47,\"f168\":12,\"f169\":1,\"f170\":9,\"f171\":100,\"f191\":-5242,\"f192\":-26632}}";

    [Fact]
    public void Parse_Single_Success()
    {
        var q = EastMoneyRealtimeSource.ParseSingle(SH600519Json, "SH600519");

        Assert.Equal("SH600519", q.Code);
        Assert.Equal("贵州茅台", q.Name);
        // f152 = 2 → 整数 / 100
        Assert.Equal(1445.41m, q.Last);
        Assert.Equal(1450.00m, q.High);
        Assert.Equal(1413.10m, q.Low);
        Assert.Equal(1413.10m, q.Open);
        Assert.Equal(1419.00m, q.PreClose);
        // f47 = 21238 手 → 2_123_800 股
        Assert.Equal(2_123_800L, q.Volume);
        Assert.Equal(3_046_049_198m, q.Amount);
        Assert.Null(q.Bid1);
        Assert.Null(q.Ask1);
        Assert.Equal("EastMoney", q.Source);
        // f86 = 1776997417 (Unix sec) → 北京时间 2026-04-24 10:23:37
        Assert.Equal(new DateTime(2026, 4, 24, 10, 23, 37), q.Timestamp);
    }

    [Fact]
    public void Parse_Batch_Sequential_Success()
    {
        // Batch 是并发 N 次单接口；这里逐条解析模拟批量。
        var q1 = EastMoneyRealtimeSource.ParseSingle(SH600519Json, "SH600519");
        var q2 = EastMoneyRealtimeSource.ParseSingle(SZ000001Json, "SZ000001");
        Assert.Equal("SH600519", q1.Code);
        Assert.Equal("SZ000001", q2.Code);
        Assert.Equal(11.01m, q2.Last);
        Assert.Equal(241850L * 100L, q2.Volume);
    }

    [Fact]
    public void Parse_NullData_Throws()
    {
        var json = "{\"rc\":102,\"data\":null}";
        var ex = Assert.Throws<DataSourceException>(() =>
            EastMoneyRealtimeSource.ParseSingle(json, "SH999999"));
        Assert.Equal("EastMoney", ex.SourceName);
        Assert.Contains("no data", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
