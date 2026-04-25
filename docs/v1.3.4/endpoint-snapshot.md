# v1.3.4 端点状态快照

发布日期：2026-04-25  
基于 commit `2e2afb4` (tag `v1.3.4`)  
测试者：Test Agent（独立全量遍历）  
测试代码：`SH600519`（贵州茅台）为主；`SZ000001`（平安银行）用于业绩快报；`anonymous/123456` 登录 baostock TCP

## 总览

| 项 | 数据 |
|---|---|
| TestUI 端点总数 | **41** |
| HTTP 200 通过 | **41 / 41** |
| 业务数据正常 | 39（PASS） |
| 业务数据空集（正常） | 2（EMPTY_DATA_OK：当日无暂停股、logout 无返回） |
| 业务/技术失败 | **0** |
| 单元测试 | 307 passed / 0 failed / 1 skipped |
| 编译告警 | 0 warning / 0 error |

## 重要约定

### 1. 响应信封（envelope）

绝大多数 POST 端点返回统一信封：

```json
{
  "ok": true,
  "rowCount": 22,
  "elapsedMs": 354,
  "sources": ["EastMoney"],
  "data": [ ... ]
}
```

`sources` 字段仅在多源对冲端点出现。

**例外**：以下 3 个 GET 端点直接返回原始对象/数组，**不带信封**：

- `GET /api/session/status` → `{isLoggedIn, isSocketConnected}`
- `GET /api/meta/endpoints` → `[ {group, path, method, description, fields} × 41 ]`
- `GET /api/loadtest/list-targets` → `[ {path, group, name, description, defaultBody} × 35 ]`

### 2. `sources` 字段（v1.3.2+）

仅在多源对冲 HTTP 端点出现，标记本次胜出的源：

| 分组 | 端点 | 候选源（按优先级） | 实测命中 |
|---|---|---|---|
| multi | `/api/multi/realtime-quote` | Sina → Tencent → EastMoney | `["Sina"]` |
| multi | `/api/multi/realtime-quotes` | 同上 | `["Sina"]` |
| multi | `/api/multi/history-k-line` | EastMoney → Tencent | `["Tencent"]` |
| financial | `/api/financial/balance-sheet` | EastMoney → Sina | `["EastMoney"]` |
| financial | `/api/financial/income-statement` | 同上 | `["EastMoney"]` |
| financial | `/api/financial/cashflow` | 同上 | `["Sina"]` |
| cninfo | `/api/cninfo/announcements` | Cninfo（单源） | `["Cninfo"]` |

baostock TCP 端点全部 `sources=null`（单源 TCP，按设计）。

### 3. 登录要求

baostock TCP 端点（`/api/baostock/*`）必须先调 `POST /api/session/login` 登录后才能访问；多源 HTTP 端点（`/api/multi/*`、`/api/financial/*`、`/api/cninfo/*`）和压测/元数据端点不需要 baostock 登录。

匿名登录即可：

```json
POST /api/session/login
{"userId":"anonymous","password":"123456"}
```

### 4. 压测 `/api/loadtest/run` 协议

**必填 `durationSeconds`（1..300）**，**不是 `requestCount`**。请求体：

```json
{
  "targetPath": "/api/multi/realtime-quote",
  "concurrency": 2,
  "durationSeconds": 3,
  "payload": "{\"code\":\"SH600519\"}"
}
```

返回 `totalRequests / successCount / errorCount / qps / latencyMs (p50/p95/avg)`。

### 5. baostock TCP 硬约束（loadtest 防护）

baostock TCP 长连接非线程安全，loadtest 层强制：

- `concurrency ≤ 1`
- `durationSeconds ≤ 30`
- 总请求量 `≤ 200`

违反硬锁返回 HTTP 400 + 明确错误。

---

## 端点详细清单

> 标识：✅ PASS（业务数据正常） / 🟡 EMPTY_DATA_OK（空集但合规） / ❌ FAIL（本版本无）

### Session（3 个）

#### 1. `POST /api/session/login` ✅
- **是否需要登录**：否（这是登录端点本身）
- **请求示例**：`{"userId":"anonymous","password":"123456"}`
- **耗时**：~200 ms
- **响应字段**：`errorCode, errorMessage, method, userId`

#### 2. `GET /api/session/status` ✅（无信封）
- **请求**：无 body
- **响应**：`{"isLoggedIn":true, "isSocketConnected":true}`

#### 3. `POST /api/session/logout` 🟡
- **请求**：`{}`
- **耗时**：~530 ms
- **响应**：`rowCount=0`，登出无数据返回（按设计）

### History（2 个，TCP，需登录）

#### 4. `POST /api/baostock/history/k-data-plus` ✅
- **请求示例**：
  ```json
  {"code":"SH600519","startDate":"2024-01-02","endDate":"2024-01-31",
   "frequency":"Day","adjustFlag":"PreAdjust","fields":""}
  ```
- **耗时**：~350 ms / **样本**：22 行（2024 年 1 月日 K）
- **响应字段**：`date, code, open, high, low, close, volume, amount, ...`

#### 5. `POST /api/baostock/history/k-data-plus-minute` ✅
- **请求示例**：同上，`frequency` 改为 `FiveMinute` / `FifteenMinute` / `ThirtyMinute` / `SixtyMinute`
- **耗时**：~750 ms / **样本**：192 行（4 个交易日 × 48 个 5min bar）

### Metadata（3 个，TCP，需登录）

#### 6. `POST /api/baostock/metadata/trade-dates` ✅
- **请求**：`{"startDate":"2024-01-01","endDate":"2024-01-31"}`
- **样本**：31 行（含周末，`isTrading` 区分）

#### 7. `POST /api/baostock/metadata/all-stock` ✅
- **请求**：`{"day":"2024-01-02"}`
- **耗时**：~2200 ms / **样本**：5638 行（全市场）
- **注意**：耗时受全量数据规模影响，是 41 端点中最慢端点

#### 8. `POST /api/baostock/metadata/stock-basic` ✅
- **请求**：`{"code":"SH600519","codeName":""}`（`code` 与 `codeName` 二选一）

### Sector（4 个，TCP，需登录）

#### 9. `POST /api/baostock/sector/stock-industry` ✅
- **请求**：`{"code":"SH600519","date":""}`（date 空=最新）

#### 10. `POST /api/baostock/sector/hs300-constituent` ✅ 300 行

#### 11. `POST /api/baostock/sector/sz50-constituent` ✅ 50 行

#### 12. `POST /api/baostock/sector/zz500-constituent` ✅ 500 行

### Evaluation（8 个，TCP，需登录）

6 个使用 `{"code":"SH600519","year":2023,"quarter":4}`（季频）：

| # | path | 耗时 | 关键字段 |
|---|---|---|---|
| 13 | `/api/baostock/evaluation/profit-data` | ~350 ms | `roeAvg, npMargin` |
| 14 | `/api/baostock/evaluation/operation-data` | ~60 ms | `nrTurnRatio, invTurnRatio` |
| 15 | `/api/baostock/evaluation/growth-data` | ~170 ms | `yoyEquity, yoyAsset, yoyNi` |
| 16 | `/api/baostock/evaluation/dupont-data` | ~50 ms | `dupontRoe, dupontAssetTurn` |
| 17 | `/api/baostock/evaluation/balance-data` | ~180 ms | `currentRatio, quickRatio, cashRatio` |
| 18 | `/api/baostock/evaluation/cash-flow-data` | ~350 ms | `caToAsset, ncaToAsset` |

#### 19. `POST /api/baostock/evaluation/dividend-data` ✅
- **请求**：`{"code":"SH600519","year":"2023","yearType":"report"}`
- **⚠️ `yearType` 必填**：取值 `"report"`（按报告期）或 `"operate"`（按经营周期）

#### 20. `POST /api/baostock/evaluation/adjust-factor` ✅
- **请求**：`{"code":"SH600519","startDate":"2024-01-01","endDate":"2024-12-31","adjustFlag":"PreAdjust"}`
- **⚠️ 用 `dateRange`，不用 `year/quarter`**

### Corp（2 个，TCP，需登录）

#### 21. `POST /api/baostock/corp/performance-express-report` ✅
- **请求**：`{"code":"SZ000001","startDate":"2020-01-01","endDate":"2024-12-31"}`
- **注意**：贵州茅台不发业绩快报；**改用平安银行 SZ000001 做覆盖**

#### 22. `POST /api/baostock/corp/forecast-report` ✅
- **请求**：`{"code":"SH600519","startDate":"2020-01-01","endDate":"2024-12-31"}`

### Macro（5 个，TCP，需登录）

5 个均接受 `{"startDate":"","endDate":""}`（空字符串 = 全量）：

| # | path | 耗时 | rows | 备注 |
|---|---|---|---|---|
| 23 | `/api/baostock/macro/deposit-rate` | ~120 ms | 43 | 历史存款利率全量 |
| 24 | `/api/baostock/macro/loan-rate` | ~420 ms | 43 | 历史贷款利率全量 |
| 25 | `/api/baostock/macro/required-reserve-ratio` | ~140 ms | 47 | 准备金率历史 |
| 26 | `/api/baostock/macro/money-supply-month` | ~1800 ms | 606 | 月度货币供应（最慢宏观端点） |
| 27 | `/api/baostock/macro/money-supply-year` | ~700 ms | 73 | 年度货币供应 |

### Special（4 个，TCP，需登录，全部 `{}` 即可）

| # | path | rows | 备注 |
|---|---|---|---|
| 28 | `/api/baostock/special/terminated-stocks` | 319 | 终止上市 |
| 29 | `/api/baostock/special/suspended-stocks` | **0** 🟡 | 当日无暂停股，正常空 |
| 30 | `/api/baostock/special/st-stocks` | 87 | ST 股 |
| 31 | `/api/baostock/special/star-st-stocks` | 89 | *ST 股 |

### Multi（3 个，HTTP 多源对冲，无需登录）

#### 32. `POST /api/multi/realtime-quote` ✅ `sources=["Sina"]`
- **请求**：`{"code":"SH600519"}`
- **三源对冲**：Sina → Tencent → EastMoney（500ms hedge）

#### 33. `POST /api/multi/realtime-quotes` ✅ `sources=["Sina"]`
- **请求**：`{"codes":["SH600519","SZ000001"]}`

#### 34. `POST /api/multi/history-k-line` ✅ `sources=["Tencent"]`
- **请求**：
  ```json
  {"code":"SH600519","frequency":"Day",
   "startDate":"2024-01-01","endDate":"2024-01-31","adjustFlag":"PreAdjust"}
  ```
- **双源对冲**：EastMoney → Tencent

### Financial（3 个，HTTP 双源对冲东财+新浪，无需登录）

请求统一 `{"code":"SH600519"}`，返回最近 10 期报告（年报+季报混合）：

| # | path | sources | 关键字段 |
|---|---|---|---|
| 35 | `/api/financial/balance-sheet` | `["EastMoney"]` | `reportDate, reportTitle, totalAssets, totalLiab, ...` |
| 36 | `/api/financial/income-statement` | `["EastMoney"]` | `totalOperateIncome（银行兜底=operateIncome）, totalProfit, netProfit, ...` |
| 37 | `/api/financial/cashflow` | `["Sina"]` | `netcashOperate, netcashInvest, netcashFinance, ...` |

### Cninfo（1 个 + 1 个 PDF 流式端点，HTTP 单源）

#### 38. `POST /api/cninfo/announcements` ✅ `sources=["Cninfo"]`
- **请求**：
  ```json
  {"code":"SH600519","category":"All",
   "startDate":"2024-01-01","endDate":"2024-12-31","pageNum":1,"pageSize":10}
  ```
- **`category` 取值**：`All / AnnualReport / SemiAnnualReport / QuarterlyReport / PerformanceForecast / TempAnnouncement`
- **响应字段含 `adjunctUrl`**，用于下方 PDF 下载

#### 附加：`GET /api/cninfo/pdf-download?adjunctUrl=...`（流式）
- **不在 `/api/meta/endpoints` 暴露**（按设计：流式响应，不是 JSON 信封）
- **支持 RFC 7233 Range（v1.3.4 完全合规）**：

| 请求 | 状态码 | 响应头 |
|---|---|---|
| 无 Range | 200 | `Accept-Ranges: bytes` |
| `Range: bytes=A-` | 206 | `Content-Range: bytes A-(total-1)/total` |
| `Range: bytes=A-B` | 206 | `Content-Range: bytes A-B/total` |
| `Range: bytes=A-B`（B 越界） | 206 | 截断到 `total-1` |
| `Range: bytes=-N`（后缀） | **416** | `Content-Range: bytes */total` |
| `Range: bytes=A-B,C-D`（多段） | **416** | 同上 |
| `Range: bytes=N-`（N 越界） | **416** | 同上 |

### Meta + LoadTest（3 个，无信封）

#### 39. `GET /api/meta/endpoints` ✅
- **响应**：41 个 endpoint 描述数组，含 `group, path, method, description, fields`
- **用途**：客户端可基于此元数据生成动态 UI 或 SDK

#### 40. `GET /api/loadtest/list-targets` ✅
- **响应**：35 个可压测目标，含 `defaultBody` 默认请求体

#### 41. `POST /api/loadtest/run` ✅
- **请求**：见上方 [压测协议](#4-压测-apiloadtestrun-协议)

---

## 性能观察

| 指标 | 端点 | 数据 |
|---|---|---|
| 最快 | `/api/baostock/evaluation/dupont-data` | 52 ms |
| 最快（HTTP 多源） | `/api/cninfo/announcements` | 289 ms |
| 最慢 | `/api/baostock/metadata/all-stock` | 2246 ms |
| 次慢 | `/api/baostock/macro/money-supply-month` | 1802 ms |
| 41 端点平均 | — | ~440 ms |

---

## 字段语义注意事项（v1.3.4 交叉验证发现）

经 16 个端点与外部权威源（东方财富 / 巨潮 / 中证指数 / PBOC / 国家统计局）交叉验证，以下字段在使用时需理解口径差异：

### 复权基准日

`/api/multi/history-k-line` 与 `/api/baostock/history/k-data-plus` 的 `PreAdjust`（前复权）数据，复权基准日是当前最近一次除权日，**与外部网页（东方财富、同花顺）的前复权数据可能存在 ~1% 价差**——因为各方复权基准日不一致。计算交易策略时建议固定一个复权口径，不要混用多源前复权数据。

### `netProfit` 净利润口径

`/api/financial/income-statement` 返回的 `netProfit` 是**合并报表净利润**（含少数股东损益）；外部公开报道常用的是**归属母公司净利润**（不含少数股东）。两者差额 = 少数股东损益。例如茅台 2024 年报：合并净利润 ≈ 281.5 亿元，归母净利润 ≈ 272.4 亿元，差额 9.1 亿元为少数股东应享。

### `dividOperateDate` 含义

`/api/baostock/evaluation/adjust-factor` 与 `/api/baostock/evaluation/dividend-data` 中的 `dividOperateDate` 是**股权登记日**（record date），不是“除权除息日”（ex-dividend date）。两者通常相差 1 个交易日。

### `loan-rate` 数据时效

`/api/baostock/macro/loan-rate` 截至 **2015-10-24**——这是中国人民银行最后一次公布“贷款基准利率”，2019 年后改为 LPR（贷款市场报价利率）。如需 LPR 数据，本接口暂不提供。

### `evaluation/dividend-data` 的 `yearType` 取值

- `"report"`：按报告期归集（2023 年报对应方案）
- `"operate"`：按经营周期归集（如跨年分红）
- 大部分 A 股公司用 `"report"` 即可

### 报告期 `reportTitle` 示例

`/api/financial/*` 三表的 `reportTitle` 取值：`"2024年年报"` / `"2024年中报"` / `"2024年三季报"` / `"2024年一季报"`。

---

## 已知限制与注意事项

1. **baostock TCP 长连接非线程安全**：单 `BaostockClient` 实例不能并发查询；多线程使用需要外部互斥或多客户端实例。
2. **巨潮 PDF 大小**：年报/半年报通常 1-5 MB，特殊公告可达 10 MB+；建议生产代码使用 `DownloadPdfToFileAsync` + `resume:true` 而非整段读入内存。
3. **多源 hedge 副作用**：实时行情每次调用都会同时对 3 个源发起请求，胜出后 cancel，对上游有放大效应；高频场景请关注源端限流。
4. **baostock 数据延迟**：财务指标按报告期，T+1 日更新；K 线收盘后 30 分钟左右补全。
5. **匿名登录限速**：baostock 服务端对 `anonymous/123456` 有连接频次限制；生产环境建议申请正式账号。

---

## 升级到 v1.3.4

```xml
<PackageReference Include="Baostock.NET" Version="1.3.4" />
```

零 BREAKING，从 v1.3.0+ 任意版本可直升。

## 相关文档

- [v1.3.4 CHANGELOG 段](../../CHANGELOG.md) — 修复明细
- [v1.3.0 专集](../v1.3.0/README.md) — 财报三表 / 巨潮 PDF 详细使用
- [v1.2.0 专集](../v1.2.0/README.md) — 多源对冲架构 / TestUI 子项目
- [README.UserAgentTest.md](../../README.UserAgentTest.md) — 交易员手动验收手册
