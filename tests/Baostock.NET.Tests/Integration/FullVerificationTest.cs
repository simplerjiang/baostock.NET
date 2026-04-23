using Baostock.NET.Client;
using Baostock.NET.Models;
using Baostock.NET.Tests.Queries;

namespace Baostock.NET.Tests.Integration;

[Collection("Live")]
[Trait("Category", "Live")]
public class FullVerificationTest
{
    private readonly LiveTestFixture _fixture;
    public FullVerificationTest(LiveTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task All_APIs_Return_Data()
    {
        var client = _fixture.Client;
        var results = new List<(string api, int rows, string? sample)>();

        // 1. K线
        var klines = await client.QueryHistoryKDataPlusAsync("sh.600000",
            startDate: "2024-01-02", endDate: "2024-01-10").ToListAsync();
        results.Add(("QueryHistoryKDataPlusAsync", klines.Count,
            klines.FirstOrDefault()?.ToString()));

        // 2. 行业分类
        var industry = await client.QueryStockIndustryAsync("sh.600000", "2024-01-02").ToListAsync();
        results.Add(("QueryStockIndustryAsync", industry.Count, industry.FirstOrDefault()?.ToString()));

        // 3. 沪深300
        var hs300 = await client.QueryHs300StocksAsync("2024-01-02").ToListAsync();
        results.Add(("QueryHs300StocksAsync", hs300.Count, hs300.FirstOrDefault()?.ToString()));

        // 4. 上证50
        var sz50 = await client.QuerySz50StocksAsync("2024-01-02").ToListAsync();
        results.Add(("QuerySz50StocksAsync", sz50.Count, sz50.FirstOrDefault()?.ToString()));

        // 5. 中证500
        var zz500 = await client.QueryZz500StocksAsync("2024-01-02").ToListAsync();
        results.Add(("QueryZz500StocksAsync", zz500.Count, zz500.FirstOrDefault()?.ToString()));

        // 6. 股息分红
        var dividend = await client.QueryDividendDataAsync("sh.600000", "2023", "report").ToListAsync();
        results.Add(("QueryDividendDataAsync", dividend.Count, dividend.FirstOrDefault()?.ToString()));

        // 7. 复权因子
        var adjust = await client.QueryAdjustFactorAsync("sh.600000", "2024-01-01", "2024-01-31").ToListAsync();
        results.Add(("QueryAdjustFactorAsync", adjust.Count, adjust.FirstOrDefault()?.ToString()));

        // 8. 季频盈利
        var profit = await client.QueryProfitDataAsync("sh.600000", 2023, 2).ToListAsync();
        results.Add(("QueryProfitDataAsync", profit.Count, profit.FirstOrDefault()?.ToString()));

        // 9. 季频营运
        var operation = await client.QueryOperationDataAsync("sh.600000", 2023, 2).ToListAsync();
        results.Add(("QueryOperationDataAsync", operation.Count, operation.FirstOrDefault()?.ToString()));

        // 10. 季频成长
        var growth = await client.QueryGrowthDataAsync("sh.600000", 2023, 2).ToListAsync();
        results.Add(("QueryGrowthDataAsync", growth.Count, growth.FirstOrDefault()?.ToString()));

        // 11. 杜邦指数
        var dupont = await client.QueryDupontDataAsync("sh.600000", 2023, 2).ToListAsync();
        results.Add(("QueryDupontDataAsync", dupont.Count, dupont.FirstOrDefault()?.ToString()));

        // 12. 偿债能力
        var balance = await client.QueryBalanceDataAsync("sh.600000", 2023, 2).ToListAsync();
        results.Add(("QueryBalanceDataAsync", balance.Count, balance.FirstOrDefault()?.ToString()));

        // 13. 现金流
        var cashflow = await client.QueryCashFlowDataAsync("sh.600000", 2023, 2).ToListAsync();
        results.Add(("QueryCashFlowDataAsync", cashflow.Count, cashflow.FirstOrDefault()?.ToString()));

        // 14. 业绩快报
        var perf = await client.QueryPerformanceExpressReportAsync("sh.600000", "2023-01-01", "2023-12-31").ToListAsync();
        results.Add(("QueryPerformanceExpressReportAsync", perf.Count, perf.FirstOrDefault()?.ToString()));

        // 15. 业绩预告
        var forecast = await client.QueryForecastReportAsync("sh.600000", "2023-01-01", "2023-12-31").ToListAsync();
        results.Add(("QueryForecastReportAsync", forecast.Count, forecast.FirstOrDefault()?.ToString()));

        // 16. 交易日
        var tradeDates = await client.QueryTradeDatesAsync("2024-01-01", "2024-01-10").ToListAsync();
        results.Add(("QueryTradeDatesAsync", tradeDates.Count, tradeDates.FirstOrDefault()?.ToString()));

        // 17. 全部证券
        var allStock = await client.QueryAllStockAsync("2024-01-02").ToListAsync();
        results.Add(("QueryAllStockAsync", allStock.Count, allStock.FirstOrDefault()?.ToString()));

        // 18. 证券基本资料
        var stockBasic = await client.QueryStockBasicAsync("sh.600000").ToListAsync();
        results.Add(("QueryStockBasicAsync", stockBasic.Count, stockBasic.FirstOrDefault()?.ToString()));

        // 19. 存款利率
        var deposit = await client.QueryDepositRateDataAsync("2023-01-01", "2023-12-31").ToListAsync();
        results.Add(("QueryDepositRateDataAsync", deposit.Count, deposit.FirstOrDefault()?.ToString()));

        // 20. 贷款利率
        var loan = await client.QueryLoanRateDataAsync("2023-01-01", "2023-12-31").ToListAsync();
        results.Add(("QueryLoanRateDataAsync", loan.Count, loan.FirstOrDefault()?.ToString()));

        // 21. 存款准备金率
        var reserve = await client.QueryRequiredReserveRatioDataAsync("2023-01-01", "2023-12-31").ToListAsync();
        results.Add(("QueryRequiredReserveRatioDataAsync", reserve.Count, reserve.FirstOrDefault()?.ToString()));

        // 22. 货币供应量（月度）
        var moneyMonth = await client.QueryMoneySupplyDataMonthAsync("2023-01", "2023-06").ToListAsync();
        results.Add(("QueryMoneySupplyDataMonthAsync", moneyMonth.Count, moneyMonth.FirstOrDefault()?.ToString()));

        // 23. 货币供应量（年度）
        var moneyYear = await client.QueryMoneySupplyDataYearAsync("2020", "2023").ToListAsync();
        results.Add(("QueryMoneySupplyDataYearAsync", moneyYear.Count, moneyYear.FirstOrDefault()?.ToString()));

        // 24. 终止上市
        var terminated = await client.QueryTerminatedStocksAsync("2024-01-02").ToListAsync();
        results.Add(("QueryTerminatedStocksAsync", terminated.Count, terminated.FirstOrDefault()?.ToString()));

        // 25. 暂停上市
        var suspended = await client.QuerySuspendedStocksAsync("2024-01-02").ToListAsync();
        results.Add(("QuerySuspendedStocksAsync", suspended.Count, suspended.FirstOrDefault()?.ToString()));

        // 26. ST 股
        var st = await client.QueryStStocksAsync("2024-01-02").ToListAsync();
        results.Add(("QueryStStocksAsync", st.Count, st.FirstOrDefault()?.ToString()));

        // 27. *ST 股
        var starst = await client.QueryStarStStocksAsync("2024-01-02").ToListAsync();
        results.Add(("QueryStarStStocksAsync", starst.Count, starst.FirstOrDefault()?.ToString()));

        // ── 输出报告 ──
        var output = new System.Text.StringBuilder();
        output.AppendLine();
        output.AppendLine(new string('=', 80));
        output.AppendLine("BAOSTOCK.NET FULL API VERIFICATION");
        output.AppendLine(new string('=', 80));

        int passCount = 0;
        foreach (var (api, rows, sample) in results)
        {
            var status = rows > 0 ? "DATA" : "EMPTY";
            if (rows > 0) passCount++;
            var sampleText = sample != null && sample.Length > 120 ? sample[..120] : sample;
            output.AppendLine($"{status} | {api,-45} | rows={rows,-6} | sample={sampleText}");
        }

        output.AppendLine(new string('=', 80));
        output.AppendLine($"TOTAL: {results.Count} APIs, {passCount} returned data, {results.Count - passCount} empty");
        output.AppendLine(new string('=', 80));

        Console.WriteLine(output.ToString());

        // 硬断言：至少 20 个 API 返回了数据（forecast_report 等可能为空）
        Assert.True(passCount >= 20, $"Only {passCount}/{results.Count} APIs returned data");
    }
}
