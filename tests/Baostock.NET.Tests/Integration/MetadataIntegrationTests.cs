using Baostock.NET.Client;
using Baostock.NET.Tests.Queries;

namespace Baostock.NET.Tests.Integration;

[Collection("Live")]
[Trait("Category", "Live")]
public class MetadataIntegrationTests
{
    private readonly LiveTestFixture _fixture;

    public MetadataIntegrationTests(LiveTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task QueryTradeDates_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryTradeDatesAsync(
            startDate: "2024-01-01",
            endDate: "2024-01-31")
            .ToListAsync();

        Assert.True(rows.Count > 0);
        Assert.True(rows[0].Date >= new DateOnly(2024, 1, 1));
    }

    [Fact]
    public async Task QueryAllStock_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryAllStockAsync(
            day: "2024-01-02")
            .ToListAsync();

        Assert.True(rows.Count > 0);
        Assert.False(string.IsNullOrEmpty(rows[0].Code));
        Assert.False(string.IsNullOrEmpty(rows[0].CodeName));
    }

    [Fact]
    public async Task QueryStockBasic_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryStockBasicAsync(
            code: "sh.600000")
            .ToListAsync();

        Assert.True(rows.Count > 0);
        Assert.Contains("sh.600000", rows[0].Code);
        Assert.False(string.IsNullOrEmpty(rows[0].CodeName));
    }
}
