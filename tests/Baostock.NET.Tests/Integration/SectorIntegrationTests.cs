using Baostock.NET.Client;
using Baostock.NET.Tests.Queries;

namespace Baostock.NET.Tests.Integration;

[Collection("Live")]
[Trait("Category", "Live")]
public class SectorIntegrationTests
{
    private readonly LiveTestFixture _fixture;

    public SectorIntegrationTests(LiveTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task QueryStockIndustry_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryStockIndustryAsync(
            code: "SH600000")
            .ToListAsync();

        Assert.True(rows.Count > 0);
        Assert.False(string.IsNullOrEmpty(rows[0].Code));
        Assert.False(string.IsNullOrEmpty(rows[0].Industry));
    }

    [Fact]
    public async Task QueryHs300Stocks_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryHs300StocksAsync()
            .ToListAsync();

        Assert.True(rows.Count > 0);
        Assert.False(string.IsNullOrEmpty(rows[0].Code));
        Assert.False(string.IsNullOrEmpty(rows[0].CodeName));
    }

    [Fact]
    public async Task QuerySz50Stocks_ReturnsRows()
    {
        var rows = await _fixture.Client.QuerySz50StocksAsync()
            .ToListAsync();

        Assert.True(rows.Count > 0);
        Assert.False(string.IsNullOrEmpty(rows[0].Code));
        Assert.False(string.IsNullOrEmpty(rows[0].CodeName));
    }

    [Fact]
    public async Task QueryZz500Stocks_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryZz500StocksAsync()
            .ToListAsync();

        Assert.True(rows.Count > 0);
        Assert.False(string.IsNullOrEmpty(rows[0].Code));
        Assert.False(string.IsNullOrEmpty(rows[0].CodeName));
    }
}
