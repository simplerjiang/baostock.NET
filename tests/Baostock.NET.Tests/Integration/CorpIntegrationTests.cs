using Baostock.NET.Client;
using Baostock.NET.Tests.Queries;

namespace Baostock.NET.Tests.Integration;

[Collection("Live")]
[Trait("Category", "Live")]
public class CorpIntegrationTests
{
    private readonly LiveTestFixture _fixture;

    public CorpIntegrationTests(LiveTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task QueryPerformanceExpressReport_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryPerformanceExpressReportAsync(
            code: "SH600000",
            startDate: "2023-01-01",
            endDate: "2023-12-31")
            .ToListAsync();

        Assert.True(rows.Count >= 0);
        if (rows.Count > 0)
            Assert.Contains("SH600000", rows[0].Code);
    }

    [Fact]
    public async Task QueryForecastReport_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryForecastReportAsync(
            code: "SH600000",
            startDate: "2023-01-01",
            endDate: "2023-12-31")
            .ToListAsync();

        Assert.True(rows.Count >= 0);
        if (rows.Count > 0)
            Assert.Contains("SH600000", rows[0].Code);
    }
}
