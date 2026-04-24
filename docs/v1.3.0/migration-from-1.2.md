> v1.3.0 专集 | 从 v1.2.0 升级 | 2026-04-24

# 从 v1.2.0 升级到 v1.3.0

## 0. 零 Breaking Change 声明

**v1.3.0 相对 v1.2.0 没有任何 BREAKING CHANGE。**

- 所有 v1.2.0 的 public API 签名、返回类型、异常契约、配置项均保持不变。
- 所有 v1.2.0 的 `Models.*Row` 字段未增删、未改名、未改类型。
- `BaostockClient.IsLoggedIn` / `IsConnected` 语义未变。
- `CodeFormatter` 输入 / 输出格式未变（仅新增 `CninfoOrgId` / `CninfoStock` 两个属性）。

升级路径：**只改 `<PackageReference>` 版本号即可**。无需代码调整。

## 1. 新增 public API 清单

### `BaostockClient` 新增方法（6 个）

| API | 所在分部类 | 源 |
|---|---|---|
| `QueryFullBalanceSheetAsync(FinancialStatementRequest, CancellationToken)` | `BaostockClient.Financials.cs` | EastMoney + Sina（hedge） |
| `QueryFullIncomeStatementAsync(FinancialStatementRequest, CancellationToken)` | `BaostockClient.Financials.cs` | EastMoney + Sina（hedge） |
| `QueryFullCashFlowAsync(FinancialStatementRequest, CancellationToken)` | `BaostockClient.Financials.cs` | EastMoney + Sina（hedge） |
| `QueryAnnouncementsAsync(CninfoAnnouncementRequest, CancellationToken)` | `BaostockClient.Cninfo.cs` | Cninfo（单源） |
| `DownloadPdfAsync(string, long?, CancellationToken)` | `BaostockClient.Cninfo.cs` | Cninfo |
| `DownloadPdfToFileAsync(string, string, bool, CancellationToken)` | `BaostockClient.Cninfo.cs` | Cninfo |

### 新增命名空间

| 命名空间 | 内容 |
|---|---|
| `Baostock.NET.Financials` | `IFinancialStatementSource` / `FinancialStatementRequest` / `FinancialReportKind` / `FinancialReportDateType` / `CompanyType` / `EastMoney*Source` / `Sina*Source`（共 6 个源类） |
| `Baostock.NET.Cninfo` | `ICninfoSource` / `CninfoSource` / `CninfoAnnouncementRequest` / `CninfoAnnouncementCategory` |

### 新增 Models

| 类型 | 字段数 |
|---|---|
| `Baostock.NET.Models.FullBalanceSheetRow` | 24 核心字段 + `RawFields` + `Source` |
| `Baostock.NET.Models.FullIncomeStatementRow` | 15 字段 + `RawFields` + `Source` |
| `Baostock.NET.Models.FullCashFlowRow` | 12 字段 + `RawFields` + `Source` |
| `Baostock.NET.Models.CninfoAnnouncementRow` | 7 字段 + `FullPdfUrl` 计算属性 |

### `CodeFormatter` 新增成员

| 成员 | 说明 |
|---|---|
| `StockCode.CninfoOrgId` | 巨潮 orgId 风格：`gss{h|z|b}0{6位代码}`（如 `gssh0600519`） |
| `StockCode.CninfoStock` | 巨潮公告查询 `stock` 参数：`{6位代码},{CninfoOrgId}`（如 `600519,gssh0600519`） |
| `CodeFormatter.ToCninfoStock(string)` | 直接从任意输入格式转换为巨潮入参 |

## 2. 如何启用财报查询（最少 5 行）

```csharp
using Baostock.NET;
using Baostock.NET.Financials;

await using var client = await BaostockClient.CreateAndLoginAsync();
var rows = await client.QueryFullBalanceSheetAsync(new FinancialStatementRequest("SH600519"));
Console.WriteLine($"{rows.Count} reports, latest totalAssets={rows[0].TotalAssets:N0} source={rows[0].Source}");
```

利润表 / 现金流量表同理，换成 `QueryFullIncomeStatementAsync` / `QueryFullCashFlowAsync`。

## 3. 如何启用巨潮公告查询与 PDF 下载（最少 5 行）

```csharp
using Baostock.NET;
using Baostock.NET.Cninfo;

await using var client = await BaostockClient.CreateAndLoginAsync();
var list = await client.QueryAnnouncementsAsync(
    new CninfoAnnouncementRequest("SH600519", CninfoAnnouncementCategory.AnnualReport, new DateOnly(2024, 1, 1)));
var bytes = await client.DownloadPdfToFileAsync(list[0].AdjunctUrl, "./report.pdf", resume: true);
```

## 4. 异常 / 错误处理（与 v1.2.0 一致）

- 财报三源对冲全挂 → `AllSourcesFailedException`（与实时行情 / 历史 K 线同一异常类）
- 巨潮查询 / PDF 失败 → `DataSourceException`（`SourceName="Cninfo"`）
- 空 `adjunctUrl` / 空 `destinationPath` → `ArgumentException`
- `request` 为 `null` → `ArgumentNullException`

## 5. 无需关注的内部变更

以下变更**不影响调用方**，仅供维护者参考：

- `BaostockClient.Financials.cs` 内私有 `FinancialStatementSourceAdapter<TRow>`：把非泛型的 `IFinancialStatementSource` 适配为 `IDataSource<FinancialStatementRequest, IReadOnlyList<TRow>>`，以便复用 v1.2.0 的 `HedgedRequestRunner<TRequest, TResult>`。
- `SourceHealthRegistry.Default` 新增两个 source 名：`"Cninfo"`（公告查询 / PDF 下载共享）；`"EastMoney"` / `"Sina"` 继续沿用（与实时行情 / 历史 K 线共享，失败计数合并统计）。
- 测试计数：v1.2.0 基线 272 passed → v1.3.0 基线 **291 passed / 0 failed / 1 skipped**（`Category!=Live`）。

## 6. 无需的改动

如果你当前项目只用 v1.2.0 能力（实时行情 / 历史 K 线 / baostock TCP），升级到 v1.3.0 后不需要做任何事情。新 API 不会影响已有调用路径，也不会额外占用 TCP 长连接。
