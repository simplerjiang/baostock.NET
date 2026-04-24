> v1.3.0 专集 | 财报三表详解 | 2026-04-24

# 财报三表（Full Financial Statements）

v1.3.0 在 `BaostockClient` 上新增 3 个财报查询 API，输出 **单期完整字段**（区别于 baostock TCP `QueryProfitDataAsync` 等只返回季频指标聚合）。底层沿用 v1.2.0 的 `HedgedRequestRunner`：东财主源（P=0）+ 新浪备用源（P=1），500ms hedge 间隔。

## 1. API 签名

三个方法均位于 `Baostock.NET.Client.BaostockClient`（`BaostockClient.Financials.cs` 分部类）：

```csharp
using Baostock.NET.Financials;
using Baostock.NET.Models;

public Task<IReadOnlyList<FullBalanceSheetRow>> QueryFullBalanceSheetAsync(
    FinancialStatementRequest request,
    CancellationToken ct = default);

public Task<IReadOnlyList<FullIncomeStatementRow>> QueryFullIncomeStatementAsync(
    FinancialStatementRequest request,
    CancellationToken ct = default);

public Task<IReadOnlyList<FullCashFlowRow>> QueryFullCashFlowAsync(
    FinancialStatementRequest request,
    CancellationToken ct = default);
```

### `FinancialStatementRequest`

```csharp
public sealed record FinancialStatementRequest(
    string Code,
    IReadOnlyList<DateOnly>? ReportDates = null,
    FinancialReportDateType DateType = FinancialReportDateType.ByReport,
    FinancialReportKind ReportKind = FinancialReportKind.Cumulative,
    CompanyType? CompanyType = null);
```

| 字段 | 含义 | 备注 |
|---|---|---|
| `Code` | 东财风格证券代码 | 兼容 `CodeFormatter` 所有输入格式 |
| `ReportDates` | 指定报告期集合 | 为 `null` 或空时由数据源自动拉取最近若干期 |
| `DateType` | 日期汇总类型 | `ByReport`（累计，Q1/Q2/Q3/Q4） / `ByYear`（仅年末） / `BySingleQuarter` |
| `ReportKind` | 报表口径 | `Cumulative`（累计） / `SingleQuarter`（单季度） |
| `CompanyType` | 公司类型 | `null` 由东财自动嗅探；显式传 `General / Bank / Insurance / Securities` 可强制 |

### 异常

- `ArgumentNullException`：`request` 为 `null`
- `AllSourcesFailedException`：东财 + 新浪双源都失败（`InnerExceptions` 聚合各源 `DataSourceException`）

## 2. 请求 / 响应示例

### 请求（资产负债表，茅台最近 N 期）

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();
var req = new FinancialStatementRequest("SH600519");
var rows = await client.QueryFullBalanceSheetAsync(req);
foreach (var r in rows.Take(3))
    Console.WriteLine($"{r.ReportDate} 总资产={r.TotalAssets:N0} 总负债={r.TotalLiabilities:N0} 来源={r.Source}");
```

### 响应（截断）

```json
[
  {
    "code": "SH600519",
    "reportDate": "2024-12-31",
    "reportTitle": "年报",
    "moneyCap": 123456789000.00,
    "accountsRece": 0.00,
    "inventory": 45678900000.00,
    "totalCurrentAssets": 234567890000.00,
    "totalAssets": 300000000000.00,
    "totalLiabilities": 50000000000.00,
    "totalParentEquity": 249999000000.00,
    "totalEquity": 250000000000.00,
    "rawFields": { "MONEY_CAP": "1.234e11", "...": "..." },
    "source": "EastMoney"
  }
]
```

货币单位统一为 **元（CNY）**。所有数值字段为 `decimal?`，允许空（不同公司类型字段集差异大）。

## 3. `FullBalanceSheetRow` 字段表（24 核心字段）

| 分组 | 字段 | 含义 |
|---|---|---|
| 标识 | `Code` / `ReportDate` / `ReportTitle` | 代码 / 报告期截止日 / 报告类型（"年报" / "一季报" / "半年报" / "三季报"） |
| 资产（流动） | `MoneyCap` | 货币资金 |
| | `TradeFinassetNotfvtpl` | 交易性金融资产 |
| | `AccountsRece` | 应收账款 |
| | `PrepaymentRece` | 预付款项 |
| | `Inventory` | 存货 |
| | `TotalCurrentAssets` | 流动资产合计 |
| 资产（非流动） | `FixedAsset` | 固定资产 |
| | `CipTotal` | 在建工程 |
| | `IntangibleAsset` | 无形资产 |
| | `TotalNoncurrentAssets` | 非流动资产合计 |
| 资产汇总 | `TotalAssets` | 资产总计 |
| 负债 | `ShortLoan` | 短期借款 |
| | `AccountsPayable` | 应付账款 |
| | `PredictLiab` | 预收款项 |
| | `TotalCurrentLiab` | 流动负债合计 |
| | `LongLoan` | 长期借款 |
| | `TotalNoncurrentLiab` | 非流动负债合计 |
| | `TotalLiabilities` | 负债合计 |
| 权益 | `ShareCapital` | 实收资本（或股本） |
| | `CapitalReserve` | 资本公积 |
| | `SurplusReserve` | 盈余公积 |
| | `UnassignProfit` | 未分配利润 |
| | `TotalParentEquity` | 归属母公司所有者权益合计 |
| | `TotalEquity` | 所有者权益合计 |
| 元信息 | `RawFields` | `IReadOnlyDictionary<string, string?>`，所有原始 key/value 兜底 |
| | `Source` | `"EastMoney"` / `"Sina"` |

## 4. `FullIncomeStatementRow` 字段表（15 字段）

| 字段 | 含义 |
|---|---|
| `Code` / `ReportDate` / `ReportTitle` | 同上 |
| `TotalOperateIncome` | 营业总收入 |
| `OperateIncome` | 营业收入 |
| `TotalOperateCost` | 营业总成本 |
| `OperateCost` | 营业成本 |
| `SaleExpense` | 销售费用 |
| `ManageExpense` | 管理费用 |
| `ResearchExpense` | 研发费用 |
| `FinanceExpense` | 财务费用 |
| `OperateProfit` | 营业利润 |
| `TotalProfit` | 利润总额 |
| `IncomeTax` | 所得税 |
| `NetProfit` | 净利润 |
| `ParentNetProfit` | 归属母公司股东净利润 |
| `BasicEps` | 基本每股收益 |
| `DilutedEps` | 稀释每股收益 |
| `RawFields` / `Source` | 同上 |

## 5. `FullCashFlowRow` 字段表（12 字段）

| 分组 | 字段 | 含义 |
|---|---|---|
| 标识 | `Code` / `ReportDate` / `ReportTitle` | 同上 |
| 经营活动 | `SalesServices` | 销售商品、提供劳务收到的现金 |
| | `TotalOperateInflow` | 经营活动现金流入小计 |
| | `TotalOperateOutflow` | 经营活动现金流出小计 |
| | `NetcashOperate` | 经营活动产生的现金流量净额 |
| 投资活动 | `TotalInvestInflow` | 投资活动现金流入小计 |
| | `TotalInvestOutflow` | 投资活动现金流出小计 |
| | `NetcashInvest` | 投资活动产生的现金流量净额 |
| 筹资活动 | `TotalFinanceInflow` | 筹资活动现金流入小计 |
| | `TotalFinanceOutflow` | 筹资活动现金流出小计 |
| | `NetcashFinance` | 筹资活动产生的现金流量净额 |
| 期末现金 | `BeginCce` | 期初现金及现金等价物余额 |
| | `EndCce` | 期末现金及现金等价物余额 |
| 元信息 | `RawFields` / `Source` | 同上 |

## 6. 东财 vs 新浪字段映射

| 报表 | 东财端点（P=0） | 新浪端点（P=1） |
|---|---|---|
| 资产负债表 | `zcfzbAjaxNew`（`datacenter-web.eastmoney.com/api/data/v1/get` 系列） | `fzb`（新浪财务页同步口） |
| 利润表 | `lrbAjaxNew` | `lrb` |
| 现金流量表 | `xjllbAjaxNew` | `llb` |

### 字段覆盖情况

- **核心字段（`TotalAssets` / `NetProfit` / `NetcashOperate` 等）**：两源基本都能返回。
- **细粒度字段（销售 / 管理 / 研发 / 财务 费用拆分、应付 / 应收拆分）**：东财覆盖更全；新浪部分字段缺失时落入 `RawFields`。
- **公司类型差异**：
  - 银行（`CompanyType.Bank`）：没有"存货"、"营业成本"这类字段；有"存放中央银行款项" / "利息收入" 等专属字段（只在 `RawFields`）。
  - 证券（`CompanyType.Securities`）：有"代理买卖证券款"等专属字段。
  - 保险（`CompanyType.Insurance`）：有"保险准备金"等专属字段。
- **兜底策略**：任何未建模字段都保留在 `RawFields` 字典，key 为东财原始驼峰字段（如 `"MONEY_CAP"`）或新浪原始 label（如 `"货币资金"`），value 为 `string?`。业务代码需要时可直接 `row.RawFields?["MONEY_CAP"]` 访问。

## 7. Hedged 机制

```
t=0       ─► EastMoney（P=0 主源）
t=500ms   ─► Sina（P=1 备用源，仅当 t=500ms 时 EastMoney 仍未返回才启动）
首个成功返回 → winner takes all，loser 进入 2s 宽限期后被取消
两源都失败    → AllSourcesFailedException
```

- **源实现**：`EastMoneyBalanceSheetSource` / `SinaBalanceSheetSource` 等实现 `IFinancialStatementSource`（非泛型，3 方法）；`BaostockClient.Financials.cs` 内的私有 `FinancialStatementSourceAdapter<TRow>` 将其适配为 `IDataSource<FinancialStatementRequest, IReadOnlyList<TRow>>` 喂给 `HedgedRequestRunner`。这样三张表共用同一套 hedging 代码，源类不必知道泛型。
- **健康冷却**：沿用 v1.2.0 的 `SourceHealthRegistry.Default`，连续 3 次失败 → 30s 冷却；冷却期该源被 `HedgedRequestRunner` 跳过。source 名固定为 `"EastMoney"` / `"Sina"`（与实时行情 / 历史 K 线共享同一命名空间）。
- **首次冷启动**：首次请求包含 HTTP 连接建立 + 双源对冲 + 500ms hedge 间隔，端到端延迟 ≤ 10s 为正常；后续同 code 命中连接池 / 健康源应 ≤ 3s。

## 8. 公司类型差异速查

| 类型 | 东财 `companyType` 参数 | 典型差异 |
|---|---|---|
| General（一般工商业，默认） | `4` | 标准 3 表，当前建模字段全适用 |
| Bank | `1` | 资产表换成 "存放同业款项 / 发放贷款和垫款" 主导；利润表主导 "利息收入 / 手续费及佣金"；建模字段大量为 null |
| Insurance | `2` | 资产表 "保险准备金"、负债表 "未到期责任准备金"；利润表 "已赚保费" |
| Securities | `3` | "代理买卖证券款"、"融出资金"；利润表 "手续费及佣金净收入" |

对于后三类，建议：

1. 调用时显式传 `CompanyType.Bank` / `Insurance` / `Securities` 让东财按正确结构返回；
2. 直接读 `row.RawFields`，不要依赖建模字段。

## 9. 代码位置

- API 入口：[`src/Baostock.NET/Client/BaostockClient.Financials.cs`](../../src/Baostock.NET/Client/BaostockClient.Financials.cs)
- 请求 / 枚举：[`src/Baostock.NET/Financials/`](../../src/Baostock.NET/Financials/) （`FinancialStatementRequest` / `FinancialReportDateType` / `FinancialReportKind` / `CompanyType` / `IFinancialStatementSource`）
- 源实现：`EastMoneyBalanceSheetSource` / `SinaBalanceSheetSource` 等 6 个文件（同目录）
- Row 模型：[`src/Baostock.NET/Models/FullBalanceSheetRow.cs`](../../src/Baostock.NET/Models/FullBalanceSheetRow.cs) / `FullIncomeStatementRow.cs` / `FullCashFlowRow.cs`
