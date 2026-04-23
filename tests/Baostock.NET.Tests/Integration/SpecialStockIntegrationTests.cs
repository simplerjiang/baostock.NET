using Baostock.NET.Client;
using Baostock.NET.Tests.Queries;

namespace Baostock.NET.Tests.Integration;

[Collection("Live")]
[Trait("Category", "Live")]
public class SpecialStockIntegrationTests
{
    private readonly LiveTestFixture _fixture;

    public SpecialStockIntegrationTests(LiveTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task QueryTerminatedStocks_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryTerminatedStocksAsync()
            .ToListAsync();

        Assert.True(rows.Count > 0);
        Assert.False(string.IsNullOrEmpty(rows[0].Code));
        Assert.False(string.IsNullOrEmpty(rows[0].CodeName));
    }

    [Fact]
    public async Task QuerySuspendedStocks_Runs()
    {
        var rows = await _fixture.Client.QuerySuspendedStocksAsync()
            .ToListAsync();

        Assert.True(rows.Count >= 0);
    }

    [Fact]
    public async Task QueryStStocks_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryStStocksAsync()
            .ToListAsync();

        Assert.True(rows.Count > 0);
        Assert.False(string.IsNullOrEmpty(rows[0].Code));
        Assert.False(string.IsNullOrEmpty(rows[0].CodeName));
    }

    [Fact]
    public async Task QueryStarStStocks_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryStarStStocksAsync()
            .ToListAsync();

        Assert.True(rows.Count > 0);
        Assert.False(string.IsNullOrEmpty(rows[0].Code));
        Assert.False(string.IsNullOrEmpty(rows[0].CodeName));
    }
}
