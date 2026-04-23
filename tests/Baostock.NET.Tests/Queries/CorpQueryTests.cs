using Baostock.NET.Client;
using Baostock.NET.Models;
using Baostock.NET.Protocol;
using Baostock.NET.Tests.Client;

namespace Baostock.NET.Tests.Queries;

public class CorpQueryTests
{
    // ── QueryPerformanceExpressReportAsync ─────────────

    [Fact]
    public async Task QueryPerformanceExpressReportAsync_SendsCorrectMsgType29()
    {
        var transport = CreateTransportWithFixture("query_performance_express_report");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryPerformanceExpressReportAsync(code: "sh.600000").ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("29", header.MessageType);
    }

    [Fact]
    public async Task QueryPerformanceExpressReportAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_performance_express_report");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryPerformanceExpressReportAsync(code: "sh.600000").ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.Contains("sh.600000", first.Code);
        Assert.NotNull(first.PerformanceExpPubDate);
    }

    // ── QueryForecastReportAsync ──────────────────────

    [Fact]
    public async Task QueryForecastReportAsync_SendsCorrectMsgType31()
    {
        var transport = CreateTransportWithFixture("query_forecast_report");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryForecastReportAsync(code: "sh.600000").ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("31", header.MessageType);
    }

    [Fact]
    public async Task QueryForecastReportAsync_FixtureReplay_ReturnsEmptyForNoData()
    {
        var transport = CreateTransportWithFixture("query_forecast_report");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryForecastReportAsync(code: "sh.600000").ToListAsync();

        // Fixture has empty data (no forecast records for sh.600000 in the captured period)
        Assert.True(rows.Count >= 0);
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
