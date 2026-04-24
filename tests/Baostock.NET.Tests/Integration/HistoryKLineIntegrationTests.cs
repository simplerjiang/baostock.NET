using Baostock.NET.Client;
using Baostock.NET.Http;
using Baostock.NET.Models;

namespace Baostock.NET.Tests.Integration;

/// <summary>
/// 多源历史 K 线对冲集成测试（v1.2.0 Sprint 2 Phase 2）。直接打 EM + 腾讯公网。
/// </summary>
public class HistoryKLineIntegrationTests
{
    private static async Task<BaostockClient> NewClientAsync()
    {
        return await BaostockClient.CreateAndLoginAsync();
    }

    [Fact]
    public async Task GetHistoryKLine_SH600519_Daily_Last30Days()
    {
        await using var client = await NewClientAsync();
        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token;
        var end = DateTime.Today;
        var start = end.AddDays(-30);

        var rows = await client.GetHistoryKLineAsync("SH600519", KLineFrequency.Day, start, end, ct: ct);

        Assert.NotEmpty(rows);
        // 30 个自然日剔除周末与节假日通常 ~18~22 根
        Assert.InRange(rows.Count, 10, 30);
        foreach (var r in rows)
        {
            Assert.Equal("SH600519", r.Code);
            Assert.InRange(r.Open, 1m, 100_000m);
            Assert.InRange(r.Close, 1m, 100_000m);
            Assert.InRange(r.High, 1m, 100_000m);
            Assert.InRange(r.Low, 1m, 100_000m);
            Assert.True(r.High >= r.Low, $"high<low at {r.Date}");
            Assert.True(r.Volume > 0, $"vol=0 at {r.Date}");
            Assert.InRange(r.Date, start.AddDays(-3), end.AddDays(1));
            Assert.Contains(r.Source, new[] { "EastMoney", "Tencent" });
        }
    }

    [Fact]
    public async Task GetHistoryKLine_SH600519_Weekly_Last1Year()
    {
        await using var client = await NewClientAsync();
        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token;
        var end = DateTime.Today;
        var start = end.AddDays(-365);

        var rows = await client.GetHistoryKLineAsync("SH600519", KLineFrequency.Week, start, end, ct: ct);

        Assert.NotEmpty(rows);
        // 一年约 52 周，剔除节假日 ~48~52 根；放宽下界以容忍 EM/Tencent 边界差异
        Assert.InRange(rows.Count, 30, 60);
        Assert.Equal("SH600519", rows[0].Code);
        Assert.True(rows[^1].Date >= rows[0].Date);
    }

    [Fact]
    public async Task GetHistoryKLine_SZ000001_Daily_NoAdjust_HasOpen()
    {
        await using var client = await NewClientAsync();
        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token;
        var end = DateTime.Today;
        var start = end.AddDays(-30);

        var rows = await client.GetHistoryKLineAsync(
            "SZ000001", KLineFrequency.Day, start, end, AdjustFlag.NoAdjust, ct);

        Assert.NotEmpty(rows);
        Assert.Equal("SZ000001", rows[0].Code);
        // 不复权下 open/close 都是合理小价（平安银行 ~10 元量级）
        foreach (var r in rows)
        {
            Assert.True(r.Open > 0m, $"open<=0 at {r.Date}");
            Assert.True(r.Close > 0m);
            Assert.InRange(r.Open, 1m, 1000m);
        }
    }

    [Fact]
    public async Task GetHistoryKLine_BJ430047_Daily_Last30Days()
    {
        // 北交所 K 线现状（v1.2.0 Sprint 2 Phase 2 实测，2026-04-24）：
        //   * 腾讯 fqkline/get 与 kline/kline 对所有 BJ 代码（bj430047/bj430139/bj873169 等）
        //     `data.{code}.day` 始终为空数组 `[]`，不返回任何历史 K 线；
        //   * 东财 push2his.eastmoney.com 在 secid=116.{code} 上稳定出现 ResponseEnded（连接被服务端中断），
        //     且 81/82/90/8/128/152.x 等其它前缀同样不通。
        //
        // 因此双源 hedge 全部失败 → 抛 AllSourcesFailedException。这是公共数据源对 BJ 历史 K 线
        // 的真实可用性限制，不是 SDK bug。本测试断言这一现状，作为「BJ K 线可用」的 tripwire：
        // 当 EM/Tencent 任一源恢复 BJ K 线后，此用例会失败，提示我们更新实现 / 移除 TODO。
        //
        // 实时行情 BJ430047 仍走 hedge fallback（Tencent 胜出），见 Phase 1 RealtimeIntegrationTests。
        await using var client = await NewClientAsync();
        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token;
        var end = DateTime.Today;
        var start = end.AddDays(-30);

        var ex = await Assert.ThrowsAsync<AllSourcesFailedException>(
            () => client.GetHistoryKLineAsync("BJ430047", KLineFrequency.Day, start, end, ct: ct));
        Assert.Equal("kline", ex.DataKind);
        Assert.Equal(2, ex.InnerExceptions.Count);
        // 至少一条内部异常来自腾讯（"no kline array" / "EMPTY"），证明请求确实路由到了 Tencent fallback。
        Assert.Contains(ex.InnerExceptions, e =>
            (e is DataSourceException dse && dse.SourceName == "Tencent")
            || e.Message.Contains("Tencent", StringComparison.OrdinalIgnoreCase));
    }
}
