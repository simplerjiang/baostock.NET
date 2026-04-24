# Baostock.NET

纯 .NET 9 客户端，对接 [baostock](https://baostock.com/) 中国 A 股市场数据服务。
完整复刻 baostock Python 客户端的全部公开 API。

[![NuGet](https://img.shields.io/nuget/v/Baostock.NET.svg)](https://www.nuget.org/packages/Baostock.NET)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Docs](https://img.shields.io/badge/docs-GitHub%20Pages-blue)](https://simplerjiang.github.io/baostock.NET/)

## 快速开始

```bash
dotnet add package Baostock.NET
```

```csharp
using Baostock.NET;

await using var client = await BaostockClient.CreateAndLoginAsync();

// K 线数据（v1.2.0 BREAKING：证券代码默认东方财富风格 SH600000 / SZ000001 / BJ430047，亦兼容 sh.600000、sh600000、600000.SH等格式）
await foreach (var row in client.QueryHistoryKDataPlusAsync("SH600000",
    startDate: "2024-01-01", endDate: "2024-01-31"))
{
    Console.WriteLine($"{row.Date} {row.Close}");
}

// 财务指标
await foreach (var row in client.QueryProfitDataAsync("SH600000", 2023, 2))
{
    Console.WriteLine($"{row.RoeAvg}");
}
```

## API 对照表

### 会话管理

| Python | .NET | 说明 |
|---|---|---|
| `bs.login()` | `BaostockClient.CreateAndLoginAsync()` | 登录（默认匿名） |
| `bs.logout()` | `client.LogoutAsync()` | 登出当前会话 |
| `bs.set_API_key()` | `client.Session.ApiKey` | 设置 API Key（属性赋值） |

### K 线 / 行情

| Python | .NET | 说明 |
|---|---|---|
| `bs.query_history_k_data_plus()` | `client.QueryHistoryKDataPlusAsync()` | K 线数据（日/周/月/分钟） |

### 财务指标（季频）

| Python | .NET | 说明 |
|---|---|---|
| `bs.query_profit_data()` | `client.QueryProfitDataAsync()` | 盈利能力 |
| `bs.query_operation_data()` | `client.QueryOperationDataAsync()` | 营运能力 |
| `bs.query_growth_data()` | `client.QueryGrowthDataAsync()` | 成长能力 |
| `bs.query_dupont_data()` | `client.QueryDupontDataAsync()` | 杜邦指数 |
| `bs.query_balance_data()` | `client.QueryBalanceDataAsync()` | 资产负债 |
| `bs.query_cash_flow_data()` | `client.QueryCashFlowDataAsync()` | 现金流量 |

### 估值 / 分红 / 复权

| Python | .NET | 说明 |
|---|---|---|
| `bs.query_dividend_data()` | `client.QueryDividendDataAsync()` | 股息分红 |
| `bs.query_adjust_factor()` | `client.QueryAdjustFactorAsync()` | 复权因子 |

### 公司公告

| Python | .NET | 说明 |
|---|---|---|
| `bs.query_performance_express_report()` | `client.QueryPerformanceExpressReportAsync()` | 业绩快报 |
| `bs.query_forecast_report()` | `client.QueryForecastReportAsync()` | 业绩预告 |

### 宏观经济

| Python | .NET | 说明 |
|---|---|---|
| `bs.query_deposit_rate_data()` | `client.QueryDepositRateDataAsync()` | 存款利率 |
| `bs.query_loan_rate_data()` | `client.QueryLoanRateDataAsync()` | 贷款利率 |
| `bs.query_required_reserve_ratio_data()` | `client.QueryRequiredReserveRatioDataAsync()` | 存款准备金率 |
| `bs.query_money_supply_data_month()` | `client.QueryMoneySupplyDataMonthAsync()` | 货币供应量（月度） |
| `bs.query_money_supply_data_year()` | `client.QueryMoneySupplyDataYearAsync()` | 货币供应量（年度） |

### 基础信息 / 日历

| Python | .NET | 说明 |
|---|---|---|
| `bs.query_trade_dates()` | `client.QueryTradeDatesAsync()` | 交易日历 |
| `bs.query_all_stock()` | `client.QueryAllStockAsync()` | 证券列表（指定日期） |
| `bs.query_stock_basic()` | `client.QueryStockBasicAsync()` | 证券基本资料 |

### 板块 / 指数成分

| Python | .NET | 说明 |
|---|---|---|
| `bs.query_stock_industry()` | `client.QueryStockIndustryAsync()` | 行业分类 |
| `bs.query_hs300_stocks()` | `client.QueryHs300StocksAsync()` | 沪深 300 成分股 |
| `bs.query_sz50_stocks()` | `client.QuerySz50StocksAsync()` | 上证 50 成分股 |
| `bs.query_zz500_stocks()` | `client.QueryZz500StocksAsync()` | 中证 500 成分股 |

### .NET 扩展（Python 包未导出）

| Python | .NET | 说明 |
|---|---|---|
| — | `client.QueryTerminatedStocksAsync()` | 终止上市股票 |
| — | `client.QuerySuspendedStocksAsync()` | 暂停上市股票 |
| — | `client.QueryStStocksAsync()` | ST 股票 |
| — | `client.QueryStarStStocksAsync()` | *ST 股票 |
| — | `client.GetRealtimeQuoteAsync(code)` | **实时行情（三源对冲：Sina → Tencent → EastMoney）** |
| — | `client.GetRealtimeQuotesAsync(codes)` | **批量实时行情** |
| — | `client.GetHistoryKLineAsync(code, freq, start, end)` | **历史 K 线（双源对冲：EastMoney → Tencent）** |

```csharp
// 实时行情：三源对冲（v1.2.0 Sprint 2）
await using var client = await BaostockClient.CreateAndLoginAsync();
var quote = await client.GetRealtimeQuoteAsync("SH600519");
Console.WriteLine($"{quote.Code} {quote.Name} 现价={quote.Last} 昨收={quote.PreClose} 来源={quote.Source}");

// 批量
var quotes = await client.GetRealtimeQuotesAsync(new[] { "SH600519", "SZ000001", "BJ430047" });

// 历史 K 线：双源对冲（v1.2.0 Sprint 2 Phase 2）
// 默认 PreAdjust 前复权；500ms hedge 间隔；EM 不响应或失败时自动切到 Tencent
var rows = await client.GetHistoryKLineAsync(
    "SH600519",
    KLineFrequency.Day,
    DateTime.Today.AddDays(-30),
    DateTime.Today);
foreach (var r in rows.Take(3))
    Console.WriteLine($"{r.Date:yyyy-MM-dd} O={r.Open} C={r.Close} H={r.High} L={r.Low} 来源={r.Source}");

// 注意：北交所（BJ*）历史 K 线在当前 EM/Tencent 公网端点均不可用，会抛 AllSourcesFailedException；
// Sprint 3 计划接入替代源补齐。详见 CHANGELOG v1.2.0-preview2 Notes。
```

## 特性

- **纯 .NET** — 无 Python 依赖、无 IPC、无嵌入解释器
- **async/await** — 全异步，`IAsyncEnumerable<T>` 流式返回大结果集
- **强类型** — 每个 query 有独立 record（`KLineRow`, `ProfitRow`, ...），字段类型推断为 `DateOnly` / `decimal` / `long`
- **自动登录** — `CreateAndLoginAsync()` 一行搞定
- **可测试** — 协议层与传输层分离，`ITransport` 可替换

## 要求

- .NET 9 SDK
- 网络可达 `public-api.baostock.com:10030`（TCP）

## 构建与测试

```bash
dotnet build
dotnet test                                    # 离线单测
dotnet test --filter "Category=Live"           # 联网集成测试
```

## License

MIT — see [LICENSE](LICENSE).

## 致谢

- [baostock](https://baostock.com/) — 免费数据服务
- baostock Python 包 — 协议参考实现
