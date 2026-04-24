using Baostock.NET.Client;
using Baostock.NET.Models;
using Baostock.NET.Protocol;
using Baostock.NET.Tests.Client;

namespace Baostock.NET.Tests.Queries;

public class MinuteHistoryQueryTests
{
    [Fact]
    public async Task QueryMinute_5min_CorrectRowCount()
    {
        var transport = CreateTransport("query_history_k_data_plus_5min");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryHistoryKDataPlusMinuteAsync(
            "SH600000",
            startDate: "2024-01-02",
            endDate: "2024-01-02",
            frequency: KLineFrequency.FiveMinute).ToListAsync();

        Assert.Equal(96, rows.Count);
    }

    [Fact]
    public async Task QueryMinute_5min_FirstRowFields()
    {
        var transport = CreateTransport("query_history_k_data_plus_5min");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryHistoryKDataPlusMinuteAsync(
            "SH600000",
            startDate: "2024-01-02",
            endDate: "2024-01-02",
            frequency: KLineFrequency.FiveMinute).ToListAsync();

        var first = rows[0];
        Assert.Equal(new DateOnly(2024, 1, 2), first.Date);
        Assert.Equal("20240102093500000", first.Time);
        Assert.Equal("SH600000", first.Code);
        Assert.NotNull(first.Open);
    }

    [Fact]
    public async Task QueryMinute_5min_SendsMsgType95()
    {
        var transport = CreateTransport("query_history_k_data_plus_5min");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryHistoryKDataPlusMinuteAsync(
            "SH600000",
            startDate: "2024-01-02",
            endDate: "2024-01-02",
            frequency: KLineFrequency.FiveMinute).ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var queryFrame = transport.SentFrames[1];
        var header = MessageHeader.Parse(queryFrame.AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("95", header.MessageType);
    }

    [Fact]
    public async Task QueryMinute_60min_CorrectRowCount()
    {
        var transport = CreateTransport("query_history_k_data_plus_60min");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryHistoryKDataPlusMinuteAsync(
            "SH600000",
            startDate: "2024-01-02",
            endDate: "2024-01-02",
            frequency: KLineFrequency.SixtyMinute).ToListAsync();

        Assert.Equal(8, rows.Count);
    }

    [Fact]
    public async Task QueryDaily_WithMinuteFrequency_ThrowsArgumentOutOfRange()
    {
        var transport = CreateTransport("query_history_k_data_plus_5min");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await client.QueryHistoryKDataPlusAsync(
                "SH600000",
                frequency: KLineFrequency.FiveMinute).ToListAsync());
    }

    [Fact]
    public async Task QueryMinute_WithDailyFrequency_ThrowsArgumentOutOfRange()
    {
        var transport = CreateTransport("query_history_k_data_plus_5min");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await client.QueryHistoryKDataPlusMinuteAsync(
                "SH600000",
                frequency: KLineFrequency.Day).ToListAsync());
    }

    private static FakeTransport CreateTransport(string fixtureName)
    {
        var transport = new FakeTransport();

        var loginFrame = FixtureLoader.StripTrailingNewline(
            FixtureLoader.Load("login", "response.bin"));
        transport.EnqueueResponse(loginFrame);

        var historyFrame = FixtureLoader.StripTrailingNewline(
            FixtureLoader.Load(fixtureName, "response.bin"));
        transport.EnqueueResponse(historyFrame);

        return transport;
    }
}
