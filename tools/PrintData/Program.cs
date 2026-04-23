using System.Text;
using Baostock.NET.Client;
using Baostock.NET.Models;

Console.OutputEncoding = Encoding.UTF8;

await using var client = await BaostockClient.CreateAndLoginAsync();
Console.WriteLine("=== Baostock.NET v1.0.0 全量数据验证 ===\n");

var apiCount = 0;
var dataCount = 0;
var emptyCount = 0;

async Task Show<T>(string name, IAsyncEnumerable<T> query, int maxRows = 3)
{
    apiCount++;
    Console.WriteLine(new string('=', 60));
    Console.WriteLine($"API: {name}");
    var rows = new List<T>();
    await foreach (var r in query)
        rows.Add(r);
    Console.WriteLine($"total rows: {rows.Count}");
    if (rows.Count == 0)
    {
        emptyCount++;
        Console.WriteLine("  (empty)");
    }
    else
    {
        dataCount++;
        for (int i = 0; i < Math.Min(rows.Count, maxRows); i++)
            Console.WriteLine($"  row[{i}]: {rows[i]}");
        if (rows.Count > maxRows)
            Console.WriteLine($"  ... ({rows.Count - maxRows} more rows)");
    }
    Console.WriteLine();
}

// K线
await Show("QueryHistoryKDataPlusAsync", client.QueryHistoryKDataPlusAsync(
    "sh.600000", startDate: "2024-01-02", endDate: "2024-01-10",
    frequency: KLineFrequency.Day, adjustFlag: AdjustFlag.PreAdjust));

// 分钟K线
await Show("QueryHistoryKDataPlusMinuteAsync (5min)", client.QueryHistoryKDataPlusMinuteAsync(
    "sh.600000", startDate: "2024-01-02", endDate: "2024-01-03",
    frequency: KLineFrequency.FiveMinute, adjustFlag: AdjustFlag.PreAdjust));

// 板块
await Show("QueryStockIndustryAsync", client.QueryStockIndustryAsync("sh.600000", "2024-01-02"));
await Show("QueryHs300StocksAsync", client.QueryHs300StocksAsync("2024-01-02"));
await Show("QuerySz50StocksAsync", client.QuerySz50StocksAsync("2024-01-02"));
await Show("QueryZz500StocksAsync", client.QueryZz500StocksAsync("2024-01-02"));

// 季频
await Show("QueryDividendDataAsync", client.QueryDividendDataAsync("sh.600000", "2023", "report"));
await Show("QueryAdjustFactorAsync", client.QueryAdjustFactorAsync("sh.600000", "2024-01-01", "2024-01-31"));
await Show("QueryProfitDataAsync", client.QueryProfitDataAsync("sh.600000", 2023, 2));
await Show("QueryOperationDataAsync", client.QueryOperationDataAsync("sh.600000", 2023, 2));
await Show("QueryGrowthDataAsync", client.QueryGrowthDataAsync("sh.600000", 2023, 2));
await Show("QueryDupontDataAsync", client.QueryDupontDataAsync("sh.600000", 2023, 2));
await Show("QueryBalanceDataAsync", client.QueryBalanceDataAsync("sh.600000", 2023, 2));
await Show("QueryCashFlowDataAsync", client.QueryCashFlowDataAsync("sh.600000", 2023, 2));

// 公告
await Show("QueryPerformanceExpressReportAsync", client.QueryPerformanceExpressReportAsync("sh.600000", "2023-01-01", "2023-12-31"));
await Show("QueryForecastReportAsync", client.QueryForecastReportAsync("sh.600000", "2023-01-01", "2023-12-31"));

// 元数据
await Show("QueryTradeDatesAsync", client.QueryTradeDatesAsync("2024-01-01", "2024-01-10"));
await Show("QueryAllStockAsync", client.QueryAllStockAsync("2024-01-02"));
await Show("QueryStockBasicAsync", client.QueryStockBasicAsync("sh.600000"));

// 宏观
await Show("QueryDepositRateDataAsync", client.QueryDepositRateDataAsync("2023-01-01", "2023-12-31"));
await Show("QueryLoanRateDataAsync", client.QueryLoanRateDataAsync("2023-01-01", "2023-12-31"));
await Show("QueryRequiredReserveRatioDataAsync", client.QueryRequiredReserveRatioDataAsync("2023-01-01", "2023-12-31"));
await Show("QueryMoneySupplyDataMonthAsync", client.QueryMoneySupplyDataMonthAsync("2023-01", "2023-06"));
await Show("QueryMoneySupplyDataYearAsync", client.QueryMoneySupplyDataYearAsync("2020", "2023"));

// 特殊股票
await Show("QueryTerminatedStocksAsync", client.QueryTerminatedStocksAsync("2024-01-02"));
await Show("QuerySuspendedStocksAsync", client.QuerySuspendedStocksAsync("2024-01-02"));
await Show("QueryStStocksAsync", client.QueryStStocksAsync("2024-01-02"));
await Show("QueryStarStStocksAsync", client.QueryStarStStocksAsync("2024-01-02"));

Console.WriteLine(new string('=', 60));
Console.WriteLine($"TOTAL: {apiCount} APIs, {dataCount} returned data, {emptyCount} empty");
Console.WriteLine("=== 验证完毕 ===");
