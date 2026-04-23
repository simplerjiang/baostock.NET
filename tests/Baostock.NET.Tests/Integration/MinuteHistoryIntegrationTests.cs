using Baostock.NET.Client;
using Baostock.NET.Models;
using Baostock.NET.Tests.Queries;

namespace Baostock.NET.Tests.Integration;

[Collection("Live")]
[Trait("Category", "Live")]
public class MinuteHistoryIntegrationTests
{
    private readonly LiveTestFixture _fixture;

    public MinuteHistoryIntegrationTests(LiveTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task QueryMinute_5min_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryHistoryKDataPlusMinuteAsync(
            "sh.600000", startDate: "2024-01-02", endDate: "2024-01-03",
            frequency: KLineFrequency.FiveMinute).ToListAsync();
        Assert.InRange(rows.Count, 90, 100); // 约96
        Assert.Equal("sh.600000", rows[0].Code);
        Assert.Equal(17, rows[0].Time.Length);
    }

    [Fact]
    public async Task QueryMinute_60min_ReturnsRows()
    {
        var rows = await _fixture.Client.QueryHistoryKDataPlusMinuteAsync(
            "sh.600000", startDate: "2024-01-02", endDate: "2024-01-03",
            frequency: KLineFrequency.SixtyMinute).ToListAsync();
        Assert.InRange(rows.Count, 6, 10); // 约8
        Assert.Equal("sh.600000", rows[0].Code);
        Assert.Equal(17, rows[0].Time.Length);
    }
}
