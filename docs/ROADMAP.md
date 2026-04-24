# Baostock.NET 扩展路线图

> 目标：将项目从 "baostock TCP 协议封装" 扩展为 "A 股综合数据 .NET SDK"
> 创建时间：2026-04-23
> 状态：规划中

---

## 已完成

### baostock TCP 协议（v1.0.0 ~ v1.0.1）
- 27 个公共 API，覆盖日K线、分钟K线、财务评估、板块、宏观、特殊股票等
- 纯 .NET 9 TCP 协议实现，无 Python 依赖
- 完整 async/await + IAsyncEnumerable&lt;T&gt;
- 20+ 强类型 Model

---

## Phase 1: 实时行情（腾讯/新浪 HTTP）

**优先级**：高  
**预估复杂度**：低

| API | URL | 功能 | 格式 | 认证 |
|-----|-----|------|------|------|
| 腾讯实时行情 | `https://qt.gtimg.cn/q={code}` | 个股/批量实时报价 | 自定义文本 | 无 |
| 新浪实时行情 | `https://hq.sinajs.cn/list={code}` | 实时报价（备用） | 自定义文本 | 需 Referer 头 |

**注意事项**：
- 腾讯接口无需任何 Header
- 新浪接口需设置 `Referer: https://finance.sina.com.cn`
- 两者均为自定义文本格式，需自行解析

---

## Phase 2: 东方财富历史行情

**优先级**：高  
**预估复杂度**：低

| API | URL | 功能 | 格式 | 认证 |
|-----|-----|------|------|------|
| 历史K线 | `https://push2his.eastmoney.com/api/qt/stock/kline/get` | 日K/周K/月K线（前/后复权） | JSON | 无 |
| 实时行情 | `https://push2.eastmoney.com/api/qt/stock/get` | 个股实时行情 | JSON | 无 |

**关键参数**：
- `secid`: `0.{code}`(深交所) 或 `1.{code}`(上交所)
- `klt`: 101=日K, 102=周K, 103=月K
- `fqt`: 0=不复权, 1=前复权, 2=后复权
- `lmt`: 返回条数

---

## Phase 3: 财务报表（东方财富 + 新浪）

**优先级**：高  
**预估复杂度**：中

### 3.1 东方财富财报 API（推荐，英文字段名）

**Base URL**: `https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/`

| API | 路径 | 功能 |
|-----|------|------|
| 公司类型获取 | `Index?type=web&code={code}` | 获取 companyType（银行=1, 保险=2, 证券=3, 一般=4） |
| 报表日期列表 | `{zcfzb\|lrb\|xjllb}DateAjaxNew` | 获取可用的报告期日期 |
| 资产负债表 | `zcfzbAjaxNew` | 完整资产负债表数据 |
| 利润表 | `lrbAjaxNew` | 完整利润表数据 |
| 现金流量表 | `xjllbAjaxNew` | 完整现金流量表数据 |

**参数**：
- `companyType`: 公司类型（1-4）
- `reportDateType`: 0=按报告期, 1=按年度, 2=按单季度
- `reportType`: 1=累计, 2=单季度
- `dates`: 逗号分隔日期（最多 5 个），如 `2025-12-31,2025-09-30`
- `code`: 股票代码，如 `SH600519`

### 3.2 新浪财经财报 API（备选，最简单）

**URL**: `https://quotes.sina.cn/cn/api/openapi.php/CompanyFinanceService.getFinanceReport2022`

| 参数 | 说明 |
|------|------|
| `paperCode` | 股票代码，如 `sh600519` |
| `source` | `fzb`=资产负债表, `lrb`=利润表, `llb`=现金流量表, `gjzb`=关键指标 |
| `type` | 0 |
| `page` | 页码 |
| `num` | 每页条数（最大 1000） |

**优点**：单一接口覆盖四种报表，无需认证，无需预获取 companyType

---

## Phase 4: 深度数据

**优先级**：中  
**预估复杂度**：低

| API | URL | 功能 | 格式 | 认证 |
|-----|-----|------|------|------|
| 券商研报 | `https://reportapi.eastmoney.com/report/list` | 研报列表（标题、机构、评级、EPS 预测） | JSON | 无 |
| 十大股东 | `https://emweb.securities.eastmoney.com/PC_HSF10/ShareholderResearch/PageAjax` | 股东人数、集中度、十大股东 | JSON | 无 |
| 龙虎榜 | `https://datacenter-web.eastmoney.com/api/data/v1/get` | 机构/游资买卖明细 | JSON | 无 |
| 宏观数据 | `https://datacenter-web.eastmoney.com/api/data/v1/get` | GDP/CPI/PMI 等 | JSON | 无 |

**龙虎榜参数示例**：
- `reportName=RPT_BILLBOARD_DAILYDETAILS`
- `sortColumns=TRADE_DATE&sortTypes=-1`
- `filter=(TRADE_DATE>'2026-04-20')`

**宏观数据参数示例**：
- `reportName=RPT_ECONOMY_GDP`（GDP）
- `reportName=RPT_ECONOMY_CPI`（CPI）

---

## Phase 5: 财报 PDF 下载（巨潮资讯）

**优先级**：中  
**预估复杂度**：低

**公告列表 API**:
- URL: `http://www.cninfo.com.cn/new/hisAnnouncement/query`
- Method: POST
- Content-Type: `application/x-www-form-urlencoded`
- 参数: `stock={code},{orgId}` + `category` + `pageNum` + `pageSize` + `column` + `tabName`

**公告分类**：
| category | 说明 |
|----------|------|
| `category_ndbg_szsh` | 年报 |
| `category_bndbg_szsh` | 半年报 |
| `category_sjdbg_szsh` | 季报 |

**orgId 规则**：
- 深交所: `gssz` + 7 位补零代码（如 `gssz0000001`）
- 上交所: `gssh` + 7 位补零代码（如 `gssh0600519`）

**PDF 下载**：
- URL: `http://static.cninfo.com.cn/` + `adjunctUrl`（从公告列表返回）
- 无需认证，直接 GET 下载
- 典型大小：年报 1~2MB，季报 200KB

---

## 实测验证记录（2026-04-23）

以下接口已在 Windows PowerShell 中实测通过：

| 接口 | 状态码 | 数据有效 |
|------|--------|----------|
| 腾讯实时行情 | 200 | ✅ |
| 新浪实时行情 | 200 | ✅ |
| 腾讯历史K线 | 200 | ✅ |
| 东方财富实时行情 | 200 | ✅ |
| 东方财富历史K线 | 200 | ✅ |
| 东方财富资产负债表 | 200 | ✅ |
| 东方财富利润表 | 200 | ✅ |
| 东方财富现金流量表 | 200 | ✅ |
| 新浪财报（四表） | 200 | ✅ |
| 东方财富研报 | 200 | ✅ |
| 东方财富十大股东 | 200 | ✅ |
| 东方财富龙虎榜 | 200 | ✅ |
| 东方财富GDP宏观 | 200 | ✅ |
| 巨潮公告列表 | 200 | ✅ |
| 巨潮PDF下载 | 200 | ✅ |
| 东方财富行业板块 | 502 | ❌ |
| 上交所信息披露 | 200 | ⚠️ 空数据 |
| 深交所信息披露 | 404 | ❌ |

---

## 轻量级财务数据 SDK 设计方案

> 状态：已设计，待开发
> 设计时间：2026-04-23

### 设计原则

1. **多源回退** — 每种数据至少 2 个源，自动切换
2. **指数退避重试** — 429/5xx 时自动等待重试（2s → 4s → 8s）
3. **流式接口** — `IAsyncEnumerable<T>` 风格，边获取边返回
4. **参数自动适配** — 用户只给股票代码（如 `sh.600000`），SDK 自动处理交易所前缀、orgId、secid、companyType 等

### API 设计

```csharp
// === 1. 结构化财报 ===
// 资产负债表（东方财富 datacenter → 新浪 → 同花顺 自动回退）
await foreach (var report in client.QueryBalanceSheetAsync("sh.600000", startDate: "2020-01-01"))
{
    Console.WriteLine($"{report.ReportDate} 总资产: {report.TotalAssets}");
}

// 利润表
await foreach (var report in client.QueryIncomeStatementAsync("sh.600000"))

// 现金流量表
await foreach (var report in client.QueryCashFlowAsync("sh.600000"))

// === 2. PDF 下载（巨潮资讯） ===
await foreach (var pdf in client.DownloadAnnouncementPdfsAsync(
    "sh.600000",
    category: AnnouncementCategory.AnnualReport,
    startDate: "2020-01-01",
    endDate: "2025-12-31"))
{
    // pdf.Title, pdf.PublishDate, pdf.Content (Stream)
    await using var fs = File.Create($"{pdf.Title}.pdf");
    await pdf.Content.CopyToAsync(fs);
}

// === 3. 券商研报 ===
await foreach (var report in client.QueryResearchReportsAsync("sh.600000", limit: 20))
{
    Console.WriteLine($"{report.Date} {report.Institution}: {report.Title}");
}

// === 4. 股东信息 ===
var shareholders = await client.QueryTopShareholdersAsync("sh.600000");

// === 5. 龙虎榜 ===
await foreach (var item in client.QueryBillboardAsync(startDate: "2026-04-01"))
```

### 稳定性设计

#### 重试策略

| 场景 | 策略 |
|------|------|
| HTTP 429 | 等待 Retry-After 头指定的时间 |
| HTTP 5xx | 指数退避：2s → 4s → 8s，最多 3 次 |
| 超时 | API 查询 10s，PDF 下载 60s |
| 连接失败 | 重试 3 次后切换到下一个数据源 |

#### 数据源回退链

| 数据类型 | 源 1 | 源 2 | 源 3 |
|----------|------|------|------|
| 财务报表 | 东方财富 datacenter | 新浪 | 同花顺 |
| 行情数据 | 东方财富 | 腾讯 | 新浪 |
| PDF 公告 | 巨潮（唯一源，加强重试） | — | — |
| 研报 | 东方财富 reportapi | — | — |
| 股东 | 东方财富 emweb | — | — |
| 龙虎榜 | 东方财富 datacenter | — | — |

#### 巨潮 API 参数修正

StockCopilot 中发现的问题及修正：

```csharp
// 1. Content-Type 必须用 form-urlencoded（不能用 JSON）
// 2. stock 参数必须包含 orgId
private static string BuildCninfoStock(string code, string exchange)
{
    var orgPrefix = exchange == "sz" ? "gssz" : "gssh";
    return $"{code},{orgPrefix}0{code}";
}

// 3. column 参数：深交所用 "szse"，上交所用 "sse"
// 4. plate 参数：6 开头用 "sh"，0/3 开头用 "sz"
```

### 与 StockCopilot 的差异

| 特性 | StockCopilot | Baostock.NET SDK |
|------|-------------|------------------|
| 定位 | 全功能数据工厂（采集+解析+校验） | 轻量级 SDK（只做数据获取） |
| PDF 解析 | 三引擎投票（Docnet/PdfPig/iText7） | 不做 PDF 解析 |
| 重试策略 | 固定 500ms 延迟 | 指数退避 + 源切换 |
| Content-Type | JSON（可能导致参数被忽略） | form-urlencoded（正确） |
| 超时 | 统一 15s | 差异化（API 10s / PDF 60s） |
| 依赖 | Docnet + PdfPig + iText7 | 无额外依赖（纯 HttpClient） |

---

## 并发压测结果（2026-04-23）

### 测试方法

使用 Python aiohttp 对每个接口分 5 个并发级别（1/5/10/20/50）压测，每级别发送 3× 并发数的请求。

### 结果汇总

| 接口 | 安全并发 | 50并发成功率 | P95延迟 | 修复要点 |
|------|---------|-------------|---------|---------|
| 腾讯实时行情 `qt.gtimg.cn` | **50+** | 100% | 247ms | 无 |
| 新浪实时行情 `hq.sinajs.cn` | **50+** | 100% | 206ms | 需 Referer 头 |
| 东方财富股东 `emweb...ShareholderResearch` | **50+** | 100% | 722ms | 无 |
| 东方财富 GDP `datacenter-web...RPT_ECONOMY_GDP` | **50+** | 100% | 522ms | 无 |
| 东方财富研报 `reportapi.eastmoney.com` | **50+** | 100% | 321ms | 必须加 `qType=0` 等参数 |
| 东方财富历史K线 `push2his.eastmoney.com` | **20** | 96.7% | 498ms | 50并发偶发断连 |
| 新浪财报 `quotes.sina.cn` | **20** | 98.7% | 759ms | 50并发连接异常 |
| 巨潮公告 `cninfo.com.cn` | **20** | 90.7% | 886ms | 加 `Accept-Encoding: gzip, deflate` + UA |
| 东方财富实时行情 `push2.eastmoney.com` | **10** | — | — | 20并发 58% 返回 502 |

### 发现的 Bug 及修复

| 问题 | 根因 | 修复 |
|------|------|------|
| 巨潮公告返回乱码 | 服务器默认返回 brotli 压缩，客户端无法解码 | 请求时加 `Accept-Encoding: gzip, deflate` 头 |
| 东方财富研报返回 400 | 缺少必填参数 `qType` | 补全参数：`qType=0&orgCode=&rptType=&author=&beginTime=&endTime=` |
| 巨潮公告 stock 参数无效 | Content-Type 用了 JSON，实际需要 form-urlencoded | 改用 `application/x-www-form-urlencoded`，stock 格式为 `{code},{orgId}` |

### SDK 限流策略建议

```
高并发组（Semaphore=50）：腾讯行情、新浪行情、研报、股东、GDP
中并发组（Semaphore=20）：东方财富K线、新浪财报、巨潮公告
低并发组（Semaphore=10）：东方财富实时行情
```

---

## 无感切换架构设计

> 核心目标：当某个数据源不可用时，调用者完全无感地获取到数据。

### 机制：Hedged Requests（对冲请求）

传统顺序回退的问题是超时叠加（3 源 × 10s = 30s 最差延迟）。

**Hedged Requests 策略**：
1. 先发优先源请求
2. 如果 500ms 内没响应，同时发第二个源
3. 再过 500ms 没响应，同时发第三个源
4. 谁先返回 200 就用谁，取消其他

| 场景 | 传统顺序回退 | Hedged Requests |
|------|------------|-----------------|
| 主源正常(200ms) | 200ms | 200ms（只发 1 请求） |
| 主源慢(2s) | 2s | 700ms（备源先返回） |
| 主源挂了 | 10s + 备源 200ms | 700ms |
| 全挂 | 30s | 11s |
| 正常请求开销 | 1 个 | 1 个 |
| 异常请求开销 | 1-3 个 | 2-3 个 |

### 健康感知

每个数据源维护健康状态：
- 连续成功：重置失败计数
- 连续失败 3 次：标记为"不健康"，冷却 30 秒
- 冷却期结束后自动恢复探测

不健康的源在对冲请求中被跳过，避免浪费请求。

### 数据格式统一

不同源返回不同格式，统一映射到标准模型：

| 源 | 字段风格 | 示例 |
|---|---|---|
| 东方财富 | 英文大写 | `TOTAL_ASSETS` → `TotalAssets` |
| 新浪 | 中文 | `资产总计` → `TotalAssets` |
| 同花顺 | 中文 + 嵌套数组 | `flashData[0]` → `TotalAssets` |

每个源实现 `IDataSource<T>` 接口，负责：
1. HTTP 请求 + 参数拼接
2. 响应解析 + 字段映射
3. 统一输出 `BalanceSheetReport` / `IncomeStatementReport` / `CashFlowReport`

### 源链配置

```
实时行情:
  [500ms 对冲间隔]
  ① 腾讯 qt.gtimg.cn        → RealtimeQuote
  ② 新浪 hq.sinajs.cn       → RealtimeQuote
  ③ 东方财富 push2           → RealtimeQuote

历史K线:
  [500ms 对冲间隔]
  ① 东方财富 push2his        → KLineData
  ② 腾讯 web.ifzq.gtimg.cn  → KLineData
  ③ baostock TCP             → KLineData（现有实现）

财务报表:
  [800ms 对冲间隔，因为财报查询本身较慢]
  ① 东方财富 emweb AjaxNew   → FinancialReport
  ② 新浪 getFinanceReport2022 → FinancialReport
  ③ 同花顺 10jqka            → FinancialReport

公告/PDF:
  [无对冲，唯一源，加强重试]
  ① 巨潮 cninfo.com.cn       → Announcement + PDF Stream
  重试策略: 3 次指数退避 (1s → 2s → 4s)

研报:
  [无对冲，唯一源]
  ① 东方财富 reportapi       → ResearchReport
```

### 调用者接口

```csharp
// 调用者完全不关心源切换
await foreach (var report in client.QueryBalanceSheetAsync("sh.600000"))
{
    Console.WriteLine($"{report.ReportDate} 总资产: {report.TotalAssets}");
    // report.DataSource 可选查看来源（"eastmoney"/"sina"/"ths"）
}

// 实时行情 — 自动选最快源
var quote = await client.GetRealtimeQuoteAsync("sh.600000");
Console.WriteLine($"最新价: {quote.Price} [from {quote.DataSource}]");

// PDF 下载 — 流式，自动重试
await foreach (var pdf in client.DownloadAnnouncementPdfsAsync("sh.600000",
    category: AnnouncementCategory.AnnualReport,
    startDate: "2020-01-01"))
{
    await using var fs = File.Create($"{pdf.Title}.pdf");
    await pdf.Content.CopyToAsync(fs);
}
```
