> v1.3.0 专集 | TestUI 新端点使用 | 2026-04-24

# TestUI 新端点（v1.3.0）

v1.3.0 在 `Baostock.NET.TestUI` 子项目上扩展了 **5 个新 HTTP 端点**，对应财报三表与巨潮公告 / PDF 能力。前端 sidebar 新增两个分组：`financial` 与 `cninfo`；公告查询成功后自动渲染 PDF 下载链接。

## 1. 端点清单

| 分组 | 路径 | Method | 协议徽章 | 说明 |
|---|---|---|---|---|
| `financial` | `/api/financial/balance-sheet` | POST | `[HTTP]` | 完整资产负债表（东财 + 新浪 hedge） |
| `financial` | `/api/financial/income-statement` | POST | `[HTTP]` | 完整利润表 |
| `financial` | `/api/financial/cashflow` | POST | `[HTTP]` | 完整现金流量表 |
| `cninfo` | `/api/cninfo/announcements` | POST | `[HTTP]` | 巨潮公告索引（单源） |
| `cninfo` | `/api/cninfo/pdf-download` | GET  | `[HTTP]` | 流式 PDF 下载（`Results.File`） |

所有端点的 `Protocol` 字段为 `"http"`，可参与 `/api/loadtest/run` 压测（受全局硬限制约束：`concurrency ≤ 100` / `duration ≤ 300s` / `total ≤ 100000`）。

> 与 v1.2.0 TestUI 一致：baostock TCP 端点有额外硬锁（`concurrency = 1` / `total ≤ 200` / `duration ≤ 30s`）；本节的 5 个端点都走 HTTP 多源，**不触发 TCP 硬锁**。

## 2. `/api/financial/balance-sheet` 示例

<!-- TODO: 截图（财报查询面板填 SH600519） -->

**请求体**：

```json
{
  "code": "SH600519",
  "reportDates": "2024-12-31,2024-09-30",
  "dateType": "ByReport",
  "reportKind": "Cumulative",
  "companyType": "Auto"
}
```

| 字段 | 类型 | 必填 | 默认 | 说明 |
|---|---|---|---|---|
| `code` | string | ✓ | `SH600519` | 东财风格证券代码 |
| `reportDates` | string[] | ✗ | `""` | 逗号分隔 `yyyy-MM-dd`；留空 → 数据源自动拉取最近若干期 |
| `dateType` | enum | ✗ | `ByReport` | `ByReport` / `ByYear` / `BySingleQuarter` |
| `reportKind` | enum | ✗ | `Cumulative` | `Cumulative` / `SingleQuarter` |
| `companyType` | enum | ✗ | `Auto` | `Auto` / `General` / `Bank` / `Insurance` / `Securities`；`Auto` 由数据源嗅探 |

**响应**（前端统一 `ApiResult` 包装）：

```json
{
  "ok": true,
  "rowCount": 2,
  "data": [
    { "code": "SH600519", "reportDate": "2024-12-31", "reportTitle": "年报", "totalAssets": 3.0e11, "source": "EastMoney", "rawFields": { "...": "..." } },
    { "code": "SH600519", "reportDate": "2024-09-30", "reportTitle": "三季报", "totalAssets": 2.8e11, "source": "EastMoney", "rawFields": { "...": "..." } }
  ],
  "durationMs": 820
}
```

`/api/financial/income-statement` 与 `/api/financial/cashflow` 字段结构完全对等，只是 `data[]` 的 row 模型换成 `FullIncomeStatementRow` / `FullCashFlowRow`。

## 3. `/api/cninfo/announcements` 示例

<!-- TODO: 截图（公告查询面板填 SH600519 + AnnualReport + 2024-01-01） -->

**请求体**：

```json
{
  "code": "SH600519",
  "category": "AnnualReport",
  "startDate": "2024-01-01",
  "endDate": "",
  "pageNum": 1,
  "pageSize": 30
}
```

| 字段 | 类型 | 必填 | 默认 | 说明 |
|---|---|---|---|---|
| `code` | string | ✓ | `SH600519` | 东财风格代码 |
| `category` | enum | ✗ | `All` | `All` / `AnnualReport` / `SemiAnnualReport` / `QuarterlyReport` / `PerformanceForecast` / `TemporaryAnnouncement` |
| `startDate` | string | ✗ | 今天 -90 天 | `yyyy-MM-dd`；留空则不限 |
| `endDate` | string | ✗ | 今天 | `yyyy-MM-dd`；留空则不限 |
| `pageNum` | int | ✗ | `1` | 从 1 开始 |
| `pageSize` | int | ✗ | `30` | 建议 ≤ 50 |

**响应**：

```json
{
  "ok": true,
  "rowCount": 3,
  "data": [
    {
      "announcementId": "1234567890",
      "code": "SH600519",
      "securityName": "贵州茅台",
      "title": "贵州茅台：2024 年年度报告",
      "publishDate": "2025-03-28",
      "category": "年报",
      "adjunctUrl": "finalpage/2025-03-28/1234567890.PDF",
      "fullPdfUrl": "http://static.cninfo.com.cn/finalpage/2025-03-28/1234567890.PDF"
    }
  ],
  "durationMs": 410
}
```

## 4. `/api/cninfo/pdf-download`（GET 流式）

<!-- TODO: 截图（公告结果列表里每行右侧的"下载 PDF"按钮） -->

这是唯一一个 **GET** 端点，便于浏览器直接 `<a href>` 触发下载，不走 `EndpointRegistry` 统一模式（后者是 `POST + ApiResult`）。

**请求**：

```
GET /api/cninfo/pdf-download?adjunctUrl=finalpage/2025-03-28/1234567890.PDF
```

**成功响应**：

- `Content-Type: application/pdf`
- `Content-Disposition`：附带文件名（末段路径 + `.pdf`，缺失时 fallback 为 `announcement-{UTC}.pdf`）
- Body：PDF 二进制流

**失败响应**：

- `400 Bad Request` + `{ "ok": false, "error": "adjunctUrl is required" }`：缺失或空白的 `adjunctUrl`
- `502 Bad Gateway` + `ProblemDetails`：巨潮 CDN 返回失败或超时（`title="cninfo pdf download failed"`）

## 5. 前端使用流程

1. 启动 TestUI：
   ```powershell
   cd c:\Users\kong\baostock.Net
   dotnet run --project src/Baostock.NET.TestUI
   ```
2. 浏览器打开 `http://localhost:5050`，点击右上角 **Login**（默认 `anonymous` / `123456`）。
3. 左侧 sidebar 滚动到底部，看到 **financial** 与 **cninfo** 两个新分组；点击任一端点名展开表单。<!-- TODO: 截图 -->
4. **财报查询**：输入 `code` → 选择 `dateType` / `reportKind` / `companyType` → 点 **Send** → 右侧查看 `rowCount` / `data[]` 展开后的字段（注意每行 `source` 字段显示胜出源）。
5. **公告查询**：输入 `code` + 选 `category` + 填 `startDate` → 点 **Send** → 结果列表每条右侧自动生成 **下载 PDF** 链接（`<a href="/api/cninfo/pdf-download?adjunctUrl=...">`）；点击即触发浏览器下载。<!-- TODO: 截图 -->
6. **压测**：切到 **压测面板** Tab，Target 选 `/api/financial/balance-sheet` 或 `/api/cninfo/announcements`，Mode=`count`、`totalRequests=30`、`concurrency=3`、`warmup=2`。**注意**：PDF 下载端点（`/api/cninfo/pdf-download`）是 GET + 大文件流，不推荐压测，会堵塞网络。

## 6. 代码位置

- 端点注册（POST）：[`src/Baostock.NET.TestUI/Endpoints/EndpointRegistry.cs`](../../src/Baostock.NET.TestUI/Endpoints/EndpointRegistry.cs) 的 `BuildFinancial()` / `BuildCninfo()`
- PDF GET 端点：[`src/Baostock.NET.TestUI/Program.cs`](../../src/Baostock.NET.TestUI/Program.cs)（搜 `/api/cninfo/pdf-download`）
- 前端渲染：`src/Baostock.NET.TestUI/wwwroot/app.js`（公告结果列表的下载链接渲染逻辑）
