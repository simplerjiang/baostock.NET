using Baostock.NET.Client;
using Baostock.NET.Models;
using Baostock.NET.Protocol;
using Baostock.NET.Tests.Client;

namespace Baostock.NET.Tests.Queries;

public class MacroQueryTests
{
    // ── QueryDepositRateDataAsync ─────────────────────

    [Fact]
    public async Task QueryDepositRateDataAsync_SendsCorrectMsgType47()
    {
        var transport = CreateTransportWithFixture("query_deposit_rate_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryDepositRateDataAsync(startDate: "2010-01-01", endDate: "2023-12-31").ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("47", header.MessageType);
    }

    [Fact]
    public async Task QueryDepositRateDataAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_deposit_rate_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryDepositRateDataAsync().ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.NotNull(first.PubDate);
        Assert.NotNull(first.DemandDepositRate);
    }

    // ── QueryLoanRateDataAsync ────────────────────────

    [Fact]
    public async Task QueryLoanRateDataAsync_SendsCorrectMsgType49()
    {
        var transport = CreateTransportWithFixture("query_loan_rate_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryLoanRateDataAsync(startDate: "2010-01-01", endDate: "2023-12-31").ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("49", header.MessageType);
    }

    [Fact]
    public async Task QueryLoanRateDataAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_loan_rate_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryLoanRateDataAsync().ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.NotNull(first.PubDate);
        Assert.NotNull(first.LoanRate6Month);
    }

    // ── QueryRequiredReserveRatioDataAsync ─────────────

    [Fact]
    public async Task QueryRequiredReserveRatioDataAsync_SendsCorrectMsgType51()
    {
        var transport = CreateTransportWithFixture("query_required_reserve_ratio_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryRequiredReserveRatioDataAsync(startDate: "2010-01-01", endDate: "2023-12-31").ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("51", header.MessageType);
    }

    [Fact]
    public async Task QueryRequiredReserveRatioDataAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_required_reserve_ratio_data");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryRequiredReserveRatioDataAsync().ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.NotNull(first.PubDate);
        Assert.NotNull(first.EffectiveDate);
    }

    // ── QueryMoneySupplyDataMonthAsync ────────────────

    [Fact]
    public async Task QueryMoneySupplyDataMonthAsync_SendsCorrectMsgType53()
    {
        var transport = CreateTransportWithFixture("query_money_supply_data_month");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryMoneySupplyDataMonthAsync(startDate: "2023-01", endDate: "2023-12").ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("53", header.MessageType);
    }

    [Fact]
    public async Task QueryMoneySupplyDataMonthAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_money_supply_data_month");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryMoneySupplyDataMonthAsync().ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.NotNull(first.StatYear);
        Assert.NotNull(first.StatMonth);
    }

    // ── QueryMoneySupplyDataYearAsync ─────────────────

    [Fact]
    public async Task QueryMoneySupplyDataYearAsync_SendsCorrectMsgType55()
    {
        var transport = CreateTransportWithFixture("query_money_supply_data_year");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        _ = await client.QueryMoneySupplyDataYearAsync(startDate: "2020", endDate: "2023").ToListAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        var header = MessageHeader.Parse(transport.SentFrames[1].AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal("55", header.MessageType);
    }

    [Fact]
    public async Task QueryMoneySupplyDataYearAsync_FixtureReplay_ReturnsRows()
    {
        var transport = CreateTransportWithFixture("query_money_supply_data_year");
        await using var client = new BaostockClient(transport, "anonymous", "123456") { AutoLogin = true };

        var rows = await client.QueryMoneySupplyDataYearAsync().ToListAsync();

        Assert.True(rows.Count > 0);
        var first = rows[0];
        Assert.NotNull(first.StatYear);
        Assert.NotNull(first.M0Year);
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
