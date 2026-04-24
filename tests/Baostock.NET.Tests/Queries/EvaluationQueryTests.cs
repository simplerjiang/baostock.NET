using Baostock.NET.Client;
using Baostock.NET.Models;
using Baostock.NET.Protocol;
using Baostock.NET.Tests.Client;

namespace Baostock.NET.Tests.Queries;

public class EvaluationQueryTests
{
    // ── QueryDividendDataAsync ────────────────────────

    [Fact]
    public async Task QueryDividendDataAsync_SendsCorrectMsgType13()
    {
        var transport = CreateTransportWithFixture("query_dividend_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryDividendDataAsync(code: "SH600000", year: "2023").ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("13", header.MessageType);
    }

    [Fact]
    public async Task QueryDividendDataAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_dividend_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryDividendDataAsync(code: "SH600000", year: "2023").ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.Contains("SH600000", first.Code);
    }

    // ── QueryAdjustFactorAsync ───────────────────────

    [Fact]
    public async Task QueryAdjustFactorAsync_SendsCorrectMsgType15()
    {
        var transport = CreateTransportWithFixture("query_adjust_factor");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryAdjustFactorAsync(code: "SH600000").ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("15", header.MessageType);
    }

    [Fact]
    public async Task QueryAdjustFactorAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_adjust_factor");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryAdjustFactorAsync(code: "SH600000").ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.Contains("SH600000", first.Code);
        Assert.NotNull(first.AdjustFactor);
    }

    // ── QueryProfitDataAsync ─────────────────────────

    [Fact]
    public async Task QueryProfitDataAsync_SendsCorrectMsgType17()
    {
        var transport = CreateTransportWithFixture("query_profit_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryProfitDataAsync(code: "SH600000", year: 2023, quarter: 2).ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("17", header.MessageType);

        // 协议体断言：季频财务接口入参 SH600000 应翻译为 sh.600000
        var bodyText = System.Text.Encoding.UTF8.GetString(transport.SentFrames[1]);
        Assert.Contains("sh.600000", bodyText);
        Assert.DoesNotContain("SH600000", bodyText);
    }

    [Fact]
    public async Task QueryProfitDataAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_profit_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryProfitDataAsync(code: "SH600000", year: 2023, quarter: 2).ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.Contains("SH600000", first.Code);
    }

    // ── QueryOperationDataAsync ──────────────────────

    [Fact]
    public async Task QueryOperationDataAsync_SendsCorrectMsgType19()
    {
        var transport = CreateTransportWithFixture("query_operation_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryOperationDataAsync(code: "SH600000", year: 2023, quarter: 2).ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("19", header.MessageType);
    }

    [Fact]
    public async Task QueryOperationDataAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_operation_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryOperationDataAsync(code: "SH600000", year: 2023, quarter: 2).ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.Contains("SH600000", first.Code);
    }

    // ── QueryGrowthDataAsync ─────────────────────────

    [Fact]
    public async Task QueryGrowthDataAsync_SendsCorrectMsgType21()
    {
        var transport = CreateTransportWithFixture("query_growth_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryGrowthDataAsync(code: "SH600000", year: 2023, quarter: 2).ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("21", header.MessageType);
    }

    [Fact]
    public async Task QueryGrowthDataAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_growth_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryGrowthDataAsync(code: "SH600000", year: 2023, quarter: 2).ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.Contains("SH600000", first.Code);
    }

    // ── QueryDupontDataAsync ─────────────────────────

    [Fact]
    public async Task QueryDupontDataAsync_SendsCorrectMsgType23()
    {
        var transport = CreateTransportWithFixture("query_dupont_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryDupontDataAsync(code: "SH600000", year: 2023, quarter: 2).ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("23", header.MessageType);
    }

    [Fact]
    public async Task QueryDupontDataAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_dupont_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryDupontDataAsync(code: "SH600000", year: 2023, quarter: 2).ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.Contains("SH600000", first.Code);
    }

    // ── QueryBalanceDataAsync ────────────────────────

    [Fact]
    public async Task QueryBalanceDataAsync_SendsCorrectMsgType25()
    {
        var transport = CreateTransportWithFixture("query_balance_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryBalanceDataAsync(code: "SH600000", year: 2023, quarter: 2).ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("25", header.MessageType);
    }

    [Fact]
    public async Task QueryBalanceDataAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_balance_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryBalanceDataAsync(code: "SH600000", year: 2023, quarter: 2).ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.Contains("SH600000", first.Code);
    }

    // ── QueryCashFlowDataAsync ───────────────────────

    [Fact]
    public async Task QueryCashFlowDataAsync_SendsCorrectMsgType27()
    {
        var transport = CreateTransportWithFixture("query_cash_flow_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryCashFlowDataAsync(code: "SH600000", year: 2023, quarter: 2).ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("27", header.MessageType);
    }

    [Fact]
    public async Task QueryCashFlowDataAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_cash_flow_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryCashFlowDataAsync(code: "SH600000", year: 2023, quarter: 2).ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.Contains("SH600000", first.Code);
    }

    // ── Helper ───────────────────────────────────────

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
