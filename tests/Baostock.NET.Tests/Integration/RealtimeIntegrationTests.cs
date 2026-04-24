using Baostock.NET.Client;

namespace Baostock.NET.Tests.Integration;

/// <summary>
/// 多源实时行情对冲集成测试（v1.2.0 Sprint 2）。直接打三家公网。
/// </summary>
public class RealtimeIntegrationTests
{
    private static async Task<BaostockClient> NewClientAsync()
    {
        // 实时行情不需要登录 baostock 服务端，但 BaostockClient 当前公开方法都挂在已登录客户端上更自然。
        // 这里仍走 CreateAndLoginAsync 与既有 Live 测试惯例一致；登录失败也不阻塞实时行情。
        return await BaostockClient.CreateAndLoginAsync();
    }

    [Fact]
    public async Task GetRealtimeQuote_SH600519_ReturnsValidData()
    {
        await using var client = await NewClientAsync();
        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token;

        var quote = await client.GetRealtimeQuoteAsync("SH600519", ct);

        Assert.Equal("SH600519", quote.Code);
        Assert.False(string.IsNullOrEmpty(quote.Name));
        Assert.InRange(quote.Last, 1m, 100_000m);
        Assert.InRange(quote.PreClose, 1m, 100_000m);
        Assert.InRange(quote.Open, 1m, 100_000m);
        Assert.InRange(quote.High, 1m, 100_000m);
        Assert.InRange(quote.Low, 1m, 100_000m);
        Assert.True(quote.Volume >= 0);
        Assert.True(quote.Amount >= 0m);
        Assert.Contains(quote.Source, new[] { "Sina", "Tencent", "EastMoney" });
    }

    [Fact]
    public async Task GetRealtimeQuotes_Batch_ReturnsAllRequested()
    {
        await using var client = await NewClientAsync();
        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token;

        var codes = new[] { "SH600519", "SZ000001", "BJ430047" };
        var quotes = await client.GetRealtimeQuotesAsync(codes, ct);

        Assert.Equal(3, quotes.Count);
        for (int i = 0; i < codes.Length; i++)
        {
            Assert.Equal(codes[i], quotes[i].Code);
            Assert.False(string.IsNullOrEmpty(quotes[i].Name));
            // Last 在停牌 / 集合竞价前可能为 0；PreClose 必为正值（活跃挂牌股）。
            Assert.True(quotes[i].PreClose > 0m || quotes[i].Last > 0m,
                $"{codes[i]} both Last and PreClose are 0 (likely delisted)");
        }
    }

    [Fact]
    public async Task GetRealtimeQuote_BeijingExchange_BJ430047()
    {
        // 北交所样本 BJ430047 = 同心传动。重点验证：
        //   1) Sina 走 bj430047（CodeFormatter.LowercaseNoDot 已支持）
        //   2) Tencent 走 bj430047
        //   3) EastMoney secid 走 116.430047（Sprint 0 未线上采样，本测试即验证用）
        // 三源任一胜出即视为通过；若全失败则 AllSourcesFailedException 暴露具体源失败明细。
        await using var client = await NewClientAsync();
        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token;

        var quote = await client.GetRealtimeQuoteAsync("BJ430047", ct);

        Assert.Equal("BJ430047", quote.Code);
        Assert.False(string.IsNullOrEmpty(quote.Name));
        // BJ430047 (诺思兰德) 实测可能停牌或集合竞价中（Last=0），但 PreClose 必为正。
        Assert.InRange(quote.PreClose, 0.01m, 10_000m);
        // Sprint 2 实测：东财 secid=116.{code} 对该 BJ 股票偶发 ResponseEnded（连接被服务端中断），
        // 但 Sina（all-zero → throw） / Tencent（成功）链路稳定 —— hedge 仍能拿到合法 PreClose。
        // 因此 BJ 链路最终胜出源应为 Tencent（详见 EastMoneyRealtimeSource secid 116 TODO）。
        Assert.NotEqual("Sina", quote.Source);
    }
}
