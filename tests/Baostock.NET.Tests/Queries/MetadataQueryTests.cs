using Baostock.NET.Client;
using Baostock.NET.Models;
using Baostock.NET.Protocol;
using Baostock.NET.Tests.Client;

namespace Baostock.NET.Tests.Queries;

public class MetadataQueryTests
{
    // ── QueryTradeDatesAsync ──────────────────────────

    [Fact]
    public async Task QueryTradeDatesAsync_SendsCorrectMsgType33()
    {
        var transport = CreateTransportWithFixture("query_trade_dates");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryTradeDatesAsync(startDate: "2024-01-01", endDate: "2024-01-15").ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("33", header.MessageType);
    }

    [Fact]
    public async Task QueryTradeDatesAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_trade_dates");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryTradeDatesAsync(startDate: "2024-01-01", endDate: "2024-01-15").ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.True(first.Date > default(DateOnly));
    }

    // ── QueryAllStockAsync ────────────────────────────

    [Fact]
    public async Task QueryAllStockAsync_SendsCorrectMsgType35()
    {
        var transport = CreateTransportWithFixture("query_all_stock");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryAllStockAsync(day: "2024-01-02").ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("35", header.MessageType);
    }

    [Fact]
    public async Task QueryAllStockAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_all_stock");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryAllStockAsync(day: "2024-01-02").ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.False(string.IsNullOrEmpty(first.Code));
        Assert.False(string.IsNullOrEmpty(first.CodeName));
    }

    // ── QueryStockBasicAsync ──────────────────────────

    [Fact]
    public async Task QueryStockBasicAsync_SendsCorrectMsgType45()
    {
        var transport = CreateTransportWithFixture("query_stock_basic");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryStockBasicAsync(code: "SH600000").ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("45", header.MessageType);

        // 协议体断言：东财风格入参 SH600000 应被 CodeFormatter 翻译为 baostock 协议格式 sh.600000
        var bodyText = System.Text.Encoding.UTF8.GetString(transport.SentFrames[1]);
        Assert.Contains("sh.600000", bodyText);
        Assert.DoesNotContain("SH600000", bodyText);
    }

    [Fact]
    public async Task QueryStockBasicAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_stock_basic");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryStockBasicAsync(code: "SH600000").ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.False(string.IsNullOrEmpty(first.Code));
        Assert.False(string.IsNullOrEmpty(first.CodeName));
        Assert.False(string.IsNullOrEmpty(first.Type));
        Assert.False(string.IsNullOrEmpty(first.Status));
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
