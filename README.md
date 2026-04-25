# Baostock.NET

纯 .NET 9 客户端，对接 [baostock](https://baostock.com/) 中国 A 股市场数据服务。
完整复刻 baostock Python 客户端的全部公开 API。

[![NuGet](https://img.shields.io/nuget/v/Baostock.NET.svg)](https://www.nuget.org/packages/Baostock.NET)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Docs](https://img.shields.io/badge/docs-GitHub%20Pages-blue)](https://simplerjiang.github.io/baostock.NET/)

> **🎉 v1.3.4 已发布**（2026-04-25，RFC 7233 完全合规 + Actions Node.js 24 升级）：416 响应 `Content-Range: bytes */<total>` 完全合规 §4.2 / GitHub Actions 升级（checkout v6 / setup-dotnet v5 / upload-artifact v7 / pages v5）应对 2026-06-02 Node.js 20 弃用 / `.gitignore` 补 `tmp_*` + 清理 17 文件。零 BREAKING，完全兼容 v1.3.3。
>
> **v1.3.3 已发布**（2026-04-25，v1.3.2 契约缺陷热修）：3 个 internal endpoint meta `method` 修正为 GET / `/api/multi/*` 三端点补齐 `sources` 字段 / `/api/cninfo/pdf-download` Range 完整支持 RFC 7233（`bytes=A-B` + `Content-Range` + `Accept-Ranges`）。零 BREAKING，完全兼容 v1.3.2。
>
> **v1.3.2 已发布**（2026-04-25，API 可观测性优化）：HTTP 多源端点响应顶层暴露 `sources` 字段（Bug-N-02） / TestUI PDF 下载支持 `Range: bytes=N-` 续传透传（Bug-N-04） / `/api/meta/endpoints` 元数据增加 `method` 字段（Bug-N-05）。零 BREAKING，完全兼容 v1.3.1。
>
> **v1.3.1 已发布**（2026-04-24，银行字段修复 + 手册补齐）：银行/券商利润表 `TotalOperateIncome` 从 `OperateIncome` 自动兜底（Finding B-ICBC）/ 手册模块 I 补 I6/I7/I8 创业板/科创板/北交所硬性用例。零 BREAKING，完全兼容 v1.3.0。
>
> **v1.3.0 发布**（2026-04-24，HTTP 多源扩展）：财报三表东财 + 新浪双源对冲 / 巨潮公告检索 + PDF 下载（Range 断点续传） / TestUI 新增 5 个端点。零 BREAKING，向后兼容 v1.2.0。
> 详见 [v1.3.0 专集](docs/v1.3.0/README.md)。
>
> **v1.2.0 发布**（2026-04-24）：新增三源实时行情 + 双源历史 K 线 + TCP 自愈 + TestUI 子项目。
> 包含 4 条 BREAKING CHANGES（代码格式 / 异常类型 / Models 输出 / IsLoggedIn 语义）。
> 详见 [v1.2.0 专集](docs/v1.2.0/README.md) 与 [CHANGELOG](CHANGELOG.md)。

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

```csharp
// 财报三表：东财 + 新浪双源对冲（v1.3.0）
using Baostock.NET.Financials;

await using var client = await BaostockClient.CreateAndLoginAsync();
var req = new FinancialStatementRequest("SH600519");
var balance = await client.QueryFullBalanceSheetAsync(req);
foreach (var r in balance.Take(3))
    Console.WriteLine($"{r.ReportDate} 总资产={r.TotalAssets:N0} 总负债={r.TotalLiabilities:N0} 来源={r.Source}");

var income = await client.QueryFullIncomeStatementAsync(req);
var cashflow = await client.QueryFullCashFlowAsync(req);
```

```csharp
// 巨潮公告检索（v1.3.0）
using Baostock.NET.Cninfo;

var cninfoReq = new CninfoAnnouncementRequest(
    Code: "SH600519",
    Category: CninfoAnnouncementCategory.AnnualReport,
    StartDate: new DateOnly(2024, 1, 1),
    EndDate: null);
var announcements = await client.QueryAnnouncementsAsync(cninfoReq);
foreach (var a in announcements.Take(5))
    Console.WriteLine($"{a.PublishDate} {a.Title} -> {a.FullPdfUrl}");
```

```csharp
// 巨潮 PDF 下载（支持断点续传）（v1.3.0）
var first = announcements[0];
// 方式 A：流式读到内存 / 上游管道
await using (var pdfStream = await client.DownloadPdfAsync(first.AdjunctUrl))
{
    // 透传、上传、入库……
}
// 方式 B：直接落盘；resume=true 时按已存在文件大小发起 Range 续传
var bytes = await client.DownloadPdfToFileAsync(
    first.AdjunctUrl,
    destinationPath: $"./pdf/{first.AnnouncementId}.pdf",
    resume: true);
Console.WriteLine($"written: {bytes} bytes");
```

## 特性

### 核心
- **纯 .NET 9** — 无 Python 依赖、无 IPC、无嵌入解释器
- **async/await** — 全异步，`IAsyncEnumerable<T>` 流式返回大结果集
- **强类型** — 每个 query 独立 record（`KLineRow`、`ProfitRow`、`RealtimeQuote`、`EastMoneyKLineRow` 等）
- **自动登录** — `CreateAndLoginAsync()` 一行搞定
- **可测试** — 协议层与传输层分离，`ITransport` 可替换

### v1.2.0 新增
- **多源 Hedged Requests** — 500ms hedge 间隔，首个成功胜出，剩余自动取消
- **SourceHealthRegistry** — 连续 3 次失败自动进入 30s 冷却，不影响其他源
- **TCP 自愈** — 半死检测（`socket.Poll` + `IsConnected` 属性），断线后自动 reconnect + relogin（CAS 线程安全，最多 1 次重试）
- **统一证券代码格式** — 东财风格 `SH600519` 为标准；`CodeFormatter` 向后兼容 `sh.600519` / `sh600519` / `600519.SH` / `1.600519` 等格式
- **TestUI 子项目** — Web 前端 + minimal API，37 endpoint + 压测面板，用于交易员手动验收 + 开发者冒烟 + 小规模压测

### v1.3.0 新增（HTTP 多源扩展）
- **财报三表完整查询** — `QueryFullBalanceSheetAsync` / `QueryFullIncomeStatementAsync` / `QueryFullCashFlowAsync`，东财 P=0 + 新浪 P=1 双源 hedge（500ms），零 BREAKING
- **巨潮公告检索 + PDF 下载** — `QueryAnnouncementsAsync` 按分类（年报 / 半年报 / 季报 / 业绩预告 / 临时公告）检索；`DownloadPdfAsync` / `DownloadPdfToFileAsync` 支持 `Range` 断点续传（`206 Partial Content`）
- **TestUI 新端点** — `/api/financial/*`（3 个）+ `/api/cninfo/announcements`（POST）+ `/api/cninfo/pdf-download`（GET 流式）

## TestUI 子项目（v1.2.0 新增）

用于交易员手动验收与开发者冒烟测试的内置 Web UI：

```bash
dotnet run --project src/Baostock.NET.TestUI
```

浏览器访问 `http://localhost:5050`，可逐项测试 37 个端点，包含 28 个 baostock TCP 接口、3 个多源 HTTP 接口、6 个内部运维接口，并支持小规模压测。

**硬约束**：baostock TCP 路径受 `concurrency ≤ 1 / duration ≤ 30s / total ≤ 200` 硬锁保护（同一 TCP 长连接非线程安全）。

详见 [TestUI 使用指南](docs/v1.2.0/testui.md) 与 [交易员测试手册](README.UserAgentTest.md)。

## 要求

- .NET 9 SDK
- 网络可达 `public-api.baostock.com:10030`（TCP，用于 `Query*Async` 系列）
- 网络可达以下 HTTP 数据源（用于 v1.2.0 多源 API `GetRealtimeQuote*Async` / `GetHistoryKLineAsync`）：
  - `hq.sinajs.cn`（Sina 实时）
  - `qt.gtimg.cn` / `web.ifzq.gtimg.cn`（Tencent 实时 + K 线）
  - `push2.eastmoney.com` / `push2his.eastmoney.com`（EastMoney 实时 + K 线）

## 构建与测试

```bash
dotnet build
dotnet test                                    # 离线单测
dotnet test --filter "Category=Live"           # 联网集成测试
```

## 文档索引

- [v1.3.0 专集](docs/v1.3.0/README.md) — 财报三表 / 巨潮 PDF / TestUI 新端点 / 从 v1.2.0 迁移
- [v1.2.0 专集](docs/v1.2.0/README.md) — 架构 / 数据源 / 迁移指南 / TestUI 使用
- [入门指南](docs/getting-started.md) — 安装、Session、IAsyncEnumerable、错误处理
- [CHANGELOG](CHANGELOG.md) — 各版本变更摘要
- [ROADMAP](docs/ROADMAP.md) — v1.3.0+ 规划（财报三表、巨潮 PDF、深度数据）
- [交易员测试手册](README.UserAgentTest.md) — 手动验收流程
- [API 参考](https://simplerjiang.github.io/baostock.NET/) — DocFX 生成的 API 文档

## License

MIT — see [LICENSE](LICENSE).

## 致谢

- [baostock](https://baostock.com/) — 免费数据服务
- baostock Python 包 — 协议参考实现
