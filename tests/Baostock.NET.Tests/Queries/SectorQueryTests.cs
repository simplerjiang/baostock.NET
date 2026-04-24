using Baostock.NET.Client;
using Baostock.NET.Models;
using Baostock.NET.Protocol;
using Baostock.NET.Tests.Client;

namespace Baostock.NET.Tests.Queries;

public class SectorQueryTests
{
    // ── QueryStockIndustryAsync ───────────────────────

    [Fact]
    public async Task QueryStockIndustryAsync_SendsCorrectMsgType59()
    {
        var transport = CreateTransportWithFixture("query_stock_industry");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryStockIndustryAsync(code: "SH600000").ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("59", header.MessageType);

        // 协议体断言：行业接口入参 SH600000 应翻译为 sh.600000
        var bodyText = System.Text.Encoding.UTF8.GetString(transport.SentFrames[1]);
        Assert.Contains("sh.600000", bodyText);
        Assert.DoesNotContain("SH600000", bodyText);
    }

    [Fact]
    public async Task QueryStockIndustryAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_stock_industry");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryStockIndustryAsync(code: "SH600000").ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.False(string.IsNullOrEmpty(first.Code));
        Assert.False(string.IsNullOrEmpty(first.CodeName));
        Assert.False(string.IsNullOrEmpty(first.Industry));
    }

    // ── QueryHs300StocksAsync ─────────────────────────

    [Fact]
    public async Task QueryHs300StocksAsync_SendsCorrectMsgType61()
    {
        var transport = CreateTransportWithFixture("query_hs300_stocks");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryHs300StocksAsync().ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("61", header.MessageType);
    }

    [Fact]
    public async Task QueryHs300StocksAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_hs300_stocks");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryHs300StocksAsync().ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.False(string.IsNullOrEmpty(first.Code));
        Assert.False(string.IsNullOrEmpty(first.CodeName));
        Assert.False(string.IsNullOrEmpty(first.UpdateDate));
    }

    // ── QuerySz50StocksAsync ──────────────────────────

    [Fact]
    public async Task QuerySz50StocksAsync_SendsCorrectMsgType63()
    {
        var transport = CreateTransportWithFixture("query_sz50_stocks");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QuerySz50StocksAsync().ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("63", header.MessageType);
    }

    [Fact]
    public async Task QuerySz50StocksAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_sz50_stocks");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QuerySz50StocksAsync().ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.False(string.IsNullOrEmpty(first.Code));
        Assert.False(string.IsNullOrEmpty(first.CodeName));
    }

    // ── QueryZz500StocksAsync ─────────────────────────

    [Fact]
    public async Task QueryZz500StocksAsync_SendsCorrectMsgType65()
    {
        var transport = CreateTransportWithFixture("query_zz500_stocks");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryZz500StocksAsync().ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("65", header.MessageType);
    }

    [Fact]
    public async Task QueryZz500StocksAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_zz500_stocks");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryZz500StocksAsync().ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.False(string.IsNullOrEmpty(first.Code));
        Assert.False(string.IsNullOrEmpty(first.CodeName));
    }

    private static FakeTransport CreateTransportWithFixture(string fixtureName)
    {
        var transport = new FakeTransport();

        var loginFrame = FixtureLoader.StripTrailingNewline(
            FixtureLoader.Load("login", "response.bin"));
        transport.EnqueueResponse(loginFrame);

        var queryFrame = FixtureLoader.StripTrailingNewline(
            FixtureLoader.Load(fixtureName, "response.bin"));
        transport.EnqueueResponse(queryFrame);

        return transport;
    }
}
