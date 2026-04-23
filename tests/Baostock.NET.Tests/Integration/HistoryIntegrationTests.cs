using Baostock.NET.Client;
using Baostock.NET.Models;
using Baostock.NET.Tests.Queries;

namespace Baostock.NET.Tests.Integration;

[Trait("Category", "Live")]
public class HistoryIntegrationTests
{
    [Fact]
    public async Task QueryHistoryKDataPlus_Daily_ReturnsRows()
    {
        await using var client = await BaostockClient.CreateAndLoginAsync();
        var rows = await client.QueryHistoryKDataPlusAsync(
            "sh.600000",
            startDate: "2024-01-01",
            endDate: "2024-01-31",
            frequency: KLineFrequency.Day,
            adjustFlag: AdjustFlag.PreAdjust)
            .ToListAsync();

        Assert.InRange(rows.Count, 15, 23); // 约 18~21 个交易日
        Assert.Equal("sh.600000", rows[0].Code);
        Assert.True(rows[0].Date >= new DateOnly(2024, 1, 2));
        Assert.NotNull(rows[0].Close);
    }
}
