using Baostock.NET.Client;
using Baostock.NET.Tests.Queries;

namespace Baostock.NET.Tests.Integration;

[Collection("Live")]
[Trait("Category", "Live")]
public class MacroIntegrationTests
{
    private readonly LiveTestFixture _fixture;

    public MacroIntegrationTests(LiveTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task QueryDepositRateData_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryDepositRateDataAsync(
            startDate: "2010-01-01", endDate: "2023-12-31")
            .ToListAsync();

        Assert.True(rows.Count > 0);
        Assert.NotNull(rows[0].PubDate);
        Assert.NotNull(rows[0].DemandDepositRate);
    }

    [Fact]
    public async Task QueryLoanRateData_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryLoanRateDataAsync(
            startDate: "2010-01-01", endDate: "2023-12-31")
            .ToListAsync();

        Assert.True(rows.Count > 0);
        Assert.NotNull(rows[0].PubDate);
        Assert.NotNull(rows[0].LoanRate6Month);
    }

    [Fact]
    public async Task QueryRequiredReserveRatioData_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryRequiredReserveRatioDataAsync(
            startDate: "2010-01-01", endDate: "2023-12-31")
            .ToListAsync();

        Assert.True(rows.Count > 0);
        Assert.NotNull(rows[0].PubDate);
        Assert.NotNull(rows[0].EffectiveDate);
    }

    [Fact]
    public async Task QueryMoneySupplyDataMonth_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryMoneySupplyDataMonthAsync(
            startDate: "2023-01", endDate: "2023-12")
            .ToListAsync();

        Assert.True(rows.Count > 0);
        Assert.NotNull(rows[0].StatYear);
        Assert.NotNull(rows[0].StatMonth);
    }

    [Fact]
    public async Task QueryMoneySupplyDataYear_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryMoneySupplyDataYearAsync(
            startDate: "2020", endDate: "2023")
            .ToListAsync();

        Assert.True(rows.Count > 0);
        Assert.NotNull(rows[0].StatYear);
        Assert.NotNull(rows[0].M0Year);
    }
}
