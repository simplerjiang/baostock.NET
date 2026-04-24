using Baostock.NET.Client;
using Baostock.NET.Models;
using Baostock.NET.Protocol;
using Baostock.NET.Tests.Client;

namespace Baostock.NET.Tests.Queries;

public class HistoryQueryTests
{
    [Fact]
    public async Task QueryHistoryKDataPlusAsync_SendsCorrectMsgType95()
    {
        var transport = CreateTransportWithLoginAndHistoryFixture();
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryHistoryKDataPlusAsync(
            "SH600000",
            startDate: "2024-01-01",
            endDate: "2024-01-31",
            frequency: KLineFrequency.Day,
            adjustFlag: AdjustFlag.PreAdjust).ToListAsync();

        // 第一帧是 login request，第二帧是 query request
        Assert.Equal(2, transport.SentFrames.Count);
        var queryFrame = transport.SentFrames[1];
        var header = MessageHeader.Parse(queryFrame.AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("95", header.MessageType);

        // 协议体断言：东财风格入参 SH600000 应被 CodeFormatter 翻译为 baostock 协议格式 sh.600000
        var bodyText = System.Text.Encoding.UTF8.GetString(queryFrame);
        Assert.Contains("sh.600000", bodyText);
        Assert.DoesNotContain("SH600000", bodyText);
    }

    [Fact]
    public async Task QueryHistoryKDataPlusAsync_ReturnsCorrectRowCount()
    {
        var transport = CreateTransportWithLoginAndHistoryFixture();
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryHistoryKDataPlusAsync(
            "SH600000",
            startDate: "2024-01-01",
            endDate: "2024-01-31",
            frequency: KLineFrequency.Day,
            adjustFlag: AdjustFlag.PreAdjust).ToListAsync();

        Assert.Equal(22, rows.Count);
    }

    [Fact]
    public async Task QueryHistoryKDataPlusAsync_FirstRow_HasCorrectFields()
    {
        var transport = CreateTransportWithLoginAndHistoryFixture();
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryHistoryKDataPlusAsync(
            "SH600000",
            startDate: "2024-01-01",
            endDate: "2024-01-31",
            frequency: KLineFrequency.Day,
            adjustFlag: AdjustFlag.PreAdjust).ToListAsync();

        var first = rows[0];
        Assert.Equal(new DateOnly(2024, 1, 2), first.Date);
        Assert.Equal("SH600000", first.Code);
        Assert.NotNull(first.Open);
        Assert.NotNull(first.Close);
        Assert.NotNull(first.Volume);
        Assert.Equal(AdjustFlag.PreAdjust, first.AdjustFlag);
        Assert.Equal(TradeStatus.Normal, first.TradeStatus);
        Assert.False(first.IsST);
    }

    [Fact]
    public async Task QueryHistoryKDataPlusAsync_AutoLogin_CallsLoginFirst()
    {
        var transport = CreateTransportWithLoginAndHistoryFixture();
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        Assert.False(client.Session.IsLoggedIn);
        _ = await client.QueryHistoryKDataPlusAsync(
            "SH600000",
            startDate: "2024-01-01",
            endDate: "2024-01-31").ToListAsync();

        Assert.True(client.Session.IsLoggedIn);
    }

    [Fact]
    public async Task QueryHistoryKDataPlusAsync_WithoutLogin_Throws()
    {
        var transport = new FakeTransport();
        await using var client = new BaostockClient(transport);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.QueryHistoryKDataPlusAsync("SH600000").ToListAsync());
    }

    private static FakeTransport CreateTransportWithLoginAndHistoryFixture()
    {
        var transport = new FakeTransport();

        // 1. Login response
        var loginFrame = FixtureLoader.StripTrailingNewline(
            FixtureLoader.Load("login", "response.bin"));
        transport.EnqueueResponse(loginFrame);

        // 2. History K data response (compressed)
        var historyFrame = FixtureLoader.StripTrailingNewline(
            FixtureLoader.Load("query_history_k_data_plus", "response.bin"));
        transport.EnqueueResponse(historyFrame);

        return transport;
    }
}

internal static class AsyncEnumerableExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }
        return list;
    }
}
