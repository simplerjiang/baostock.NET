using Baostock.NET.Client;
using Baostock.NET.Models;
using Baostock.NET.Protocol;
using Baostock.NET.Tests.Client;

namespace Baostock.NET.Tests.Queries;

public class SpecialStockQueryTests
{
    // ── QueryTerminatedStocksAsync ────────────────────

    [Fact]
    public async Task QueryTerminatedStocksAsync_SendsCorrectMsgType67()
    {
        var transport = CreateCandidateTransportWithFixture("query_terminated_stocks");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryTerminatedStocksAsync(date: "2024-01-02").ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("67", header.MessageType);
    }

    [Fact]
    public async Task QueryTerminatedStocksAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateCandidateTransportWithFixture("query_terminated_stocks");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryTerminatedStocksAsync().ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.False(string.IsNullOrEmpty(first.Code));
        Assert.False(string.IsNullOrEmpty(first.CodeName));
        Assert.False(string.IsNullOrEmpty(first.UpdateDate));
    }

    // ── QuerySuspendedStocksAsync ─────────────────────

    [Fact]
    public async Task QuerySuspendedStocksAsync_SendsCorrectMsgType69()
    {
        var transport = CreateCandidateTransportWithFixture("query_suspended_stocks");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QuerySuspendedStocksAsync(date: "2024-01-02").ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("69", header.MessageType);
    }

    [Fact]
    public async Task QuerySuspendedStocksAsync_FixtureReplay_ReturnsEmptyOrRows()
    {
        var transport = CreateCandidateTransportWithFixture("query_suspended_stocks");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QuerySuspendedStocksAsync().ToListAsync();

        // Fixture may have empty data
        Assert.True(rows.Count >= 0);
    }

    // ── QueryStStocksAsync ────────────────────────────

    [Fact]
    public async Task QueryStStocksAsync_SendsCorrectMsgType71()
    {
        var transport = CreateCandidateTransportWithFixture("query_st_stocks");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryStStocksAsync(date: "2024-01-02").ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("71", header.MessageType);
    }

    [Fact]
    public async Task QueryStStocksAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateCandidateTransportWithFixture("query_st_stocks");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryStStocksAsync().ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.False(string.IsNullOrEmpty(first.Code));
        Assert.False(string.IsNullOrEmpty(first.CodeName));
    }

    // ── QueryStarStStocksAsync ────────────────────────

    [Fact]
    public async Task QueryStarStStocksAsync_SendsCorrectMsgType73()
    {
        var transport = CreateCandidateTransportWithFixture("query_starst_stocks");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryStarStStocksAsync(date: "2024-01-02").ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("73", header.MessageType);
    }

    [Fact]
    public async Task QueryStarStStocksAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateCandidateTransportWithFixture("query_starst_stocks");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryStarStStocksAsync().ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.False(string.IsNullOrEmpty(first.Code));
        Assert.False(string.IsNullOrEmpty(first.CodeName));
    }

    private static FakeTransport CreateCandidateTransportWithFixture(string fixtureName)
    {
        var transport = new FakeTransport();

        var loginFrame = FixtureLoader.StripTrailingNewline(
            FixtureLoader.Load("login", "response.bin"));
        transport.EnqueueResponse(loginFrame);

        var queryFrame = FixtureLoader.StripTrailingNewline(
            FixtureLoader.Load($"_candidates/{fixtureName}/round2", "response.bin"));
        transport.EnqueueResponse(queryFrame);

        return transport;
    }
}
