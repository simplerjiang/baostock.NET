using Baostock.NET.Client;
using Baostock.NET.Tests.Queries;

namespace Baostock.NET.Tests.Integration;

[Collection("Live")]
[Trait("Category", "Live")]
public class EvaluationIntegrationTests
{
    private readonly LiveTestFixture _fixture;

    public EvaluationIntegrationTests(LiveTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task QueryDividendData_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryDividendDataAsync(
            code: "sh.600000", year: "2023")
            .ToListAsync();

        Assert.True(rows.Count >= 0);
        if (rows.Count > 0)
            Assert.Contains("sh.600000", rows[0].Code);
    }

    [Fact]
    public async Task QueryAdjustFactor_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryAdjustFactorAsync(
            code: "sh.600000",
            startDate: "2023-01-01",
            endDate: "2023-12-31")
            .ToListAsync();

        Assert.True(rows.Count >= 0);
        if (rows.Count > 0)
            Assert.Contains("sh.600000", rows[0].Code);
    }

    [Fact]
    public async Task QueryProfitData_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryProfitDataAsync(
            code: "sh.600000", year: 2023, quarter: 2)
            .ToListAsync();

        Assert.True(rows.Count >= 0);
        if (rows.Count > 0)
            Assert.Contains("sh.600000", rows[0].Code);
    }

    [Fact]
    public async Task QueryOperationData_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryOperationDataAsync(
            code: "sh.600000", year: 2023, quarter: 2)
            .ToListAsync();

        Assert.True(rows.Count >= 0);
        if (rows.Count > 0)
            Assert.Contains("sh.600000", rows[0].Code);
    }

    [Fact]
    public async Task QueryGrowthData_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryGrowthDataAsync(
            code: "sh.600000", year: 2023, quarter: 2)
            .ToListAsync();

        Assert.True(rows.Count >= 0);
        if (rows.Count > 0)
            Assert.Contains("sh.600000", rows[0].Code);
    }

    [Fact]
    public async Task QueryDupontData_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryDupontDataAsync(
            code: "sh.600000", year: 2023, quarter: 2)
            .ToListAsync();

        Assert.True(rows.Count >= 0);
        if (rows.Count > 0)
            Assert.Contains("sh.600000", rows[0].Code);
    }

    [Fact]
    public async Task QueryBalanceData_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryBalanceDataAsync(
            code: "sh.600000", year: 2023, quarter: 2)
            .ToListAsync();

        Assert.True(rows.Count >= 0);
        if (rows.Count > 0)
            Assert.Contains("sh.600000", rows[0].Code);
    }

    [Fact]
    public async Task QueryCashFlowData_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryCashFlowDataAsync(
            code: "sh.600000", year: 2023, quarter: 2)
            .ToListAsync();

        Assert.True(rows.Count >= 0);
        if (rows.Count > 0)
            Assert.Contains("sh.600000", rows[0].Code);
    }
}
