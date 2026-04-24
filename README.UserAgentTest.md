# Baostock.NET 用户验收测试手册

> 版本：v1.3.0（含 Sprint 3 财报三表 + 巨潮公告/PDF）
> 最近更新：2026-04-24

## 适用范围

本手册供 **User Representative Agent / 真实交易员** 在 v1.2.0 已交付能力上做人工复测。
覆盖：

- 28 个 baostock TCP query API（`/api/baostock/*`）
- 3 个多源 hedge HTTP API（`/api/multi/*`）
- TestUI 前端（API 调用 Tab + 压测面板）
- `POST /api/loadtest/run` 进程内压测器

不涉及：单元测试 / 集成测试（已由 CI 跑过 267/0/2）。本手册聚焦**端到端浏览器实操**与**对外可观察行为**。

---

## 启动准备

### 1) 释放占用端口（5050）

```powershell
Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
```

> 注意：会强杀所有 `dotnet` 进程。如本机有其它 .NET 服务正在跑，请改用按 PID kill。

### 2) 启动 TestUI

```powershell
cd C:\Users\kong\baostock.Net
dotnet run --no-build -c Release --project src/Baostock.NET.TestUI
```

启动成功标志：

```
Now listening on: http://localhost:5050
Application started. Press Ctrl+C to shut down.
```

浏览器打开 <http://localhost:5050>。顶部应显示 “未登录” 状态条 + 两个 Tab（API 调用 / 压测面板）。

### 3) 健康检查

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:5050/api/session/status
```

期望：HTTP 200，body `{"isLoggedIn":false,...}`。

---

## 已知约束（先看完再测，避免踩坑）

### A. baostock TCP 端点不可并发压测

`BaostockClient` 是**单例 + 单条共享 TCP 长连接**，协议非线程安全。
`concurrency > 1` 会击毙会话，需重启进程才能恢复。

- 后端 `/api/loadtest/run` 已对 `targetPath = /api/baostock/*` 做硬拦截：
  - `concurrency > 1` → HTTP 400
  - `concurrency = 1` 但 `totalRequests > 200` 或 `durationSeconds > 30s` → HTTP 400
- 前端压测面板选中 TCP 端点时，concurrency 输入框被锁到 max=1，会显示 `[TCP] concurrency locked to 1` 提示。
- v1.3.0 计划：`BaostockClient` TCP 自愈（B1，Sprint 3 P0）。

**本手册的压测剧本只对 `/api/multi/*` 端点跑高并发。**

### B. 北交所（BJ）数据可能陈旧

- BJ K 线公网双源（EastMoney `secid=116.{c}` / Tencent `fqkline`）当前都不可用。
  `GetHistoryKLineAsync("BJ430047", ...)` 会抛 `AllSourcesFailedException`。
- BJ 实时只能拿到 Tencent 的盘前价（北交所流动性差，Sina 经常返回 all-zero）。
- 验收时若拿到的 BJ 实时 `timestamp` 早于今天 09:00，请记为 **“BJ 数据陈旧”已知问题**，不要标 blocker。

### C. 登录态 vs 实际 socket 状态可能脱节

v1.2.0-preview3 已知缺陷：socket 死后 `/api/session/login` 仍返回 `ok=true`，但实际 baostock query 会抛 `IOException`。
处理：遇到 `IOException` 风暴时，**Stop-Process 释放后重启服务**。

> **v1.2.0-preview5 起**：`BaostockClient` 已内置 TCP 自愈（socket 半死自动重连 + relogin 一次）。`/api/session/status` 新增 `isSocketConnected` 字段可直观看到底层连接健康。一般不再需要重启服务；若仍连续抛 `reconnect_failed` / `relogin_failed`，说明上游 baostock 服务器不可达，才需人工介入。

> **logout 后 `isSocketConnected=true` 是正常的**：logout 只告诉 baostock “这个会话结束”，不会主动拆掉 TCP 连接（避免下次重连延迟）。所以 `{isLoggedIn:false, isSocketConnected:true}` 是**预期状态**。要真正断开 TCP，`Dispose` 整个 `BaostockClient` 实例即可。

### D. PowerShell 直接调用 baostock 端点须显式指定日期范围

**PowerShell 直接调用 baostock 端点不带 `startDate`/`endDate` 时，后端会回退到 baostock 协议默认值**（如 `startDate=2015-01-01`），导致返回数据量爆炸（实测 `/api/baostock/metadata/trade-dates` 不传日期返回 **4132 行** 而非预期 15 行）。建议每次 PowerShell 调用都显式指定日期范围（例如 `-Body '{"startDate":"2026-03-25","endDate":"2026-04-24"}'`）。UI 已用动态日期默认值（今天前 30 天~今天）避免此问题。

---

## 测试模块

> 每个模块的步骤都假定 “已 Login 成功”（用户名 `anonymous`，密码 `123456`，匿名权限够覆盖本手册全部场景）。

### 模块 A — 早盘日历 + 茅台基础信息

| # | 操作 | 期望 | 失败分流 |
|---|---|---|---|
| A1 | 选 `metadata / QueryTradeDatesAsync`，使用默认 startDate（今天-30 天）/ endDate（今天），点 Send | `ok=true`，`rowCount > 0`，data 含 30 条左右记录，`is_trading_day` 字段有 0/1 | 若 `rowCount=0` → 检查日期是否落在节假日全段；若 `ok=false IOException` → socket 死，重启服务 |
| A2 | 选 `metadata / QueryStockBasicAsync`，code=`SH600519`，Send | `ok=true rowCount=1`，data[0].code_name 含 “贵州茅台” | 若返回 0 行 → 报告为 blocker（基础接口失效） |

### 模块 B — 历史 K 线（多源）

| # | 操作 | 期望 | 失败分流 |
|---|---|---|---|
| B1 | 选 `multi / GetHistoryKLineAsync`，code=`SH600519`，frequency=`Day`，startDate=今天-60 天，endDate=今天，adjust=`PreAdjust`，Send | `ok=true`，`rowCount` ≈ 40，`source` 显示 `EastMoney` 或 `Tencent`，最后一条 close 价合理（约 1300~1700） | 若 `source=Tencent` 但全部行 `turnoverRate=null` → 正常（Tencent 不返回换手率）；若 EM/Tencent 都失败 → blocker |
| B2 | 同上但 code=`BJ430047` | **预期失败** `ok=false errorType=AllSourcesFailedException` | 若反而成功 → 报 BJ K 线源已恢复（更新已知问题清单） |

### 模块 C — 实时报价

| # | 操作 | 期望 | 失败分流 |
|---|---|---|---|
| C1 | 选 `multi / GetRealtimeQuoteAsync`，code=`SH600519`，Send | `ok=true rowCount=1`，data.source 多为 `Sina`，`lastPrice` > 0 | source 全部走 EM 源 → 报 hedge 退化（minor） |
| C2 | 选 `multi / GetRealtimeQuotesAsync`，codes 改为 `["SH600519","SZ000001","BJ430047"]`，Send | `ok=true rowCount=3`，前 2 条 source=Sina/Tencent，BJ 那条 source 多为 Tencent | BJ 拿不到 → 检查 timestamp，若早于今天则记 “BJ 数据陈旧” |
| C3 | 选 `multi / GetRealtimeQuotesAsync`，codes 字段**清空**为 `[]`，Send | `ok=false`，`error` 包含 `codes is required and must be non-empty`（ArgumentException 实际返回附带 ` (Parameter 'codes')` 后缀，使用 Contains 语义校验；**不允许回退到默认股票**） | 若返回 ok=true 数据 → blocker（产品级错误：用户拿到不是自己请求的数据） |

### 模块 D — 财务报表（季频）

| # | 操作 | 期望 | 失败分流 |
|---|---|---|---|
| D1 | 选 `evaluation / QueryProfitDataAsync`，code=`SH600519`，year=`2024`，quarter=`1`，Send | `ok=true rowCount=1`，roeAvg > 0.05 | rowCount=0 → baostock 该季度数据未发布；rowCount>1 → 异常 |
| D2 | 默认 year/quarter（应为 当前年/上一完整季度），Send | `ok=true` 或 `rowCount=0`（若财报未发布）；不应返回 2023/Q4 这种过时默认值 | 若默认值仍是 2023 → metadata 动态化失败，blocker |

### 模块 E — 压测基线（仅 multi）

| # | 操作 | 期望 | 失败分流 |
|---|---|---|---|
| E1 | 切到 “压测面板”，Target 选 `multi / GetRealtimeQuoteAsync`，Mode=`count`，TotalRequests=`30`，Concurrency=`3`，Warmup=`2`，开始压测 | `ok=true`，QPS ≥ 5，错误率 ≤ 10%，p95 < 2000ms | 错误率 > 30% → 数据源不稳定（minor，记入报告） |
| E2 | 切到 TCP 端点（如 `metadata / QueryTradeDatesAsync`） | UI 应自动锁 concurrency=1，TotalRequests 默认改 50，duration max=30s，banner 出现 `⚠️ TCP 端点：concurrency 锁定 1, total≤200, duration≤30s` | 锁定失效 → minor（后端会兜底拦截） |

### 模块 F — 边界拒绝（必须全部 400/拒绝）

| # | 操作 | 期望 |
|---|---|---|
| F1 | 直接 curl `POST /api/loadtest/run`，body `{"targetPath":"/api/baostock/metadata/trade-dates","concurrency":2,"mode":"count","totalRequests":10}` | HTTP 400 + `error` 含 `concurrency > 1 ... non-thread-safe` |
| F2 | 直接 curl，body `{"targetPath":"/api/baostock/metadata/trade-dates","concurrency":1,"mode":"count","totalRequests":300}` | HTTP 400 + `error` 含 `heavy load (>200 requests or >30s duration)` |
| F3 | 同时双开两个压测（连续两次 POST `/api/loadtest/run`，第二个不等第一个完成） | 第二个返回 HTTP 409 `another load test is running` |
| F4 | curl `POST /api/loadtest/run` body `{"targetPath":"...","concurrency":200,...}` | HTTP 400 `concurrency must be 1..100` |

### 模块 H — 财报三表（v1.3.0 新增，HTTP 多源对冲）

> 硬规则：**至少 2 轮 UR 验证**（第二轮建议换一只银行 / 证券股，例如 `SZ000001` / `SH601398` / `SH600030`，触发 `CompanyType` 自动嗅探）。

| # | 操作 | 期望 | 失败分流 |
|---|---|---|---|
| H1 | 左侧 sidebar 切到 `financial` 分组，选 `QueryFullBalanceSheetAsync`，code=`SH600519`，其余默认（`reportDates` 留空、`dateType=ByReport`、`reportKind=Cumulative`、`companyType=Auto`），点 **Send** | `ok=true`，`rowCount ≥ 4`（近几年 4~8 份报告），每条含 `reportDate` / `totalAssets` / `totalLiabilities` / `totalEquity` 非 null，`source` 为 `EastMoney` 或 `Sina` | 超时或返回 0 行 → 直接记 bug，**不 retry**；若仅 `source=Sina` 全覆盖 → 记 hedge 退化（minor） |
| H2 | 选 `QueryFullIncomeStatementAsync`，code=`SH600519`，`reportDates`=`2024-12-31,2024-09-30`（逗号分隔），Send | `ok=true rowCount=2`，两条分别对应年报 / 三季报，`totalOperateIncome` / `netProfit` / `parentNetProfit` 非 null；`reportTitle` 含 "年报" / "三季报" 字样 | reportDates 未生效（返回 4+ 条）→ 记 bug |
| H3 | 选 `QueryFullCashFlowAsync`，code=`SZ000001`（平安银行，银行类）—— **触发公司类型差异**，Send | `ok=true rowCount ≥ 1`；允许大量字段为 null（银行现金流量表结构与一般工商业差异大），但 `rawFields` 字典不为空，`netcashOperate` 通常有值 | `rawFields` 也为空 → 记 bug（解析失败） |
| H4 | 性能观察：任一上述端点 Send，**首次** ≤ 10s（含对冲 + 500ms hedge 间隔 + 冷启动），**后续同 code** ≤ 3s | 首次 timeout（> 10s 但仍 ok=true）仅记 minor | 持续 timeout 或 `errorType=AllSourcesFailedException` → blocker |

### 模块 I — 巨潮公告 + PDF 下载（v1.3.0 新增，单源）

> 硬规则：**至少 2 轮 UR 验证**（第二轮换 `SZ000001` + `category=SemiAnnualReport`，验证 `column` 参数按交易所切换正确）。

| # | 操作 | 期望 | 失败分流 |
|---|---|---|---|
| I1 | 左侧 sidebar 切到 `cninfo` 分组，选 `QueryAnnouncementsAsync`，code=`SH600519`，`category=AnnualReport`，`startDate=2024-01-01`，`endDate` 留空 / 今天，`pageNum=1`，`pageSize=30`，Send | `ok=true rowCount ≥ 1`，`data[]` 每条含 `announcementId` / `title`（含 "年报"）/ `publishDate` / `adjunctUrl`（以 `finalpage/` 之类路径开头）/ `fullPdfUrl`（以 `http://static.cninfo.com.cn/` 开头） | 列表为空（rowCount=0）→ 记 bug（贵州茅台近年必有年报） |
| I2 | 前端自动在 I1 的结果旁渲染每一行的 **下载链接**（指向 `/api/cninfo/pdf-download?adjunctUrl=...`）。点击第一条的下载链接 | 浏览器开始下载 PDF，文件名以 `.pdf` 结尾 | 下载链接没渲染 → 记 minor（前端 bug，后端可用 curl 直连）；点击后 HTTP 502 `cninfo pdf download failed` → 检查网络到 `static.cninfo.com.cn`，连续 3 次失败记 blocker |
| I3 | 下载完成后用 PDF reader（Edge / Acrobat）打开文件 | 文件大小 **> 100KB**（年报一般 1MB+），正文可阅读，无乱码 | 文件大小 < 10KB 或打不开 → blocker（极可能下到 HTML 错误页） |
| I4 | 分类筛选验证：改 `category=SemiAnnualReport`，其余同 I1 | 返回的 `title` 全部含 "半年度报告" 或 "半年报" | 出现其他类型 → 记 bug（分类参数没生效） |
| I5 | 失败分流演练：code=`SZ000001`，`category=QuarterlyReport`，`startDate=2024-01-01`，Send | `ok=true`，rowCount ≥ 1；若为 0 → 按预期行为处理（某些公司季报不一定全披露），不算 bug | — |

### Part G — 健康态快速自检（启动后 smoke test）

五步 curl 序列，3 秒完成，用于每次启动服务后快速确认健康：

```powershell
# 1. 未登录状态
Invoke-WebRequest -UseBasicParsing http://localhost:5050/api/session/status
# 期望：{isLoggedIn:false, isSocketConnected:false}

# 2. Login
$login = Invoke-WebRequest -UseBasicParsing -Method Post -ContentType 'application/json' -Body '{}' http://localhost:5050/api/session/login
# 期望：ok=true

# 3. baostock query（会触发 TCP 连接）
$tradeDates = Invoke-WebRequest -UseBasicParsing -Method Post -ContentType 'application/json' -Body '{"startDate":"2026-01-01","endDate":"2026-01-10"}' http://localhost:5050/api/baostock/metadata/trade-dates
# 期望：ok=true, rowCount>0

# 4. 状态确认（现在两者都应 true）
Invoke-WebRequest -UseBasicParsing http://localhost:5050/api/session/status
# 期望：{isLoggedIn:true, isSocketConnected:true}

# 5. Logout（TCP 不拆，仅登出会话）
Invoke-WebRequest -UseBasicParsing -Method Post -ContentType 'application/json' -Body '{}' http://localhost:5050/api/session/logout
# 期望：ok=true；后续 /api/session/status 应 {isLoggedIn:false, isSocketConnected:true}（TCP 保留）
```

---

## 失败分流（Triage）

| 现象 | 分级 | 处理 |
|---|---|---|
| `IOException` 风暴 + 后续所有 baostock 请求都失败 | **blocker** | Stop-Process + 重启，记录前置压测/操作 |
| `multi/*` 全部 source=Sina 且其它源都缺 | **major** | 标 “hedge 退化未触发”，提 issue 附时间窗 |
| `multi/*` 某源 5xx / parse error 单次出现 | **minor** | 记数，不阻塞 |
| BJ 数据 timestamp 早于今天 09:00 | **minor (已知)** | 记 “BJ 数据陈旧已知问题” |
| metadata 默认日期是 2024-01-01 等过时值 | **blocker** | N1 修复回退，立即报 |
| `/api/multi/realtime-quotes` 空 codes 返回 ok=true | **blocker** | M1 修复回退，立即报 |
| 前端 TCP 端点未自动锁 concurrency | **minor** | M3 前端兜底失效，但后端 B2 仍会拦 |
| 后端 baostock concurrency=2 未拦截 | **blocker** | B2 修复回退 |

---

## 改进建议提交格式

UR 验收完后请用此结构产出报告（建议直接以 `docs/UR-Acceptance-{date}.md` 提交 PR）：

```
## 摘要
- 通过 / 全部模块数：X/6
- 阻塞问题数：N
- 高优问题数：N
- 低优问题数：N

## Blocker（阻塞）
### B-001 简短标题
- 复现步骤：1. ... 2. ...
- 期望：...
- 实际：...
- 截图/响应片段：...

## Major（高优）
（同上结构，编号 M-xxx）

## Minor（低优）
（同上结构，编号 N-xxx）

## 数据样本
- 测试时间窗：YYYY-MM-DD HH:MM~HH:MM
- 命中股票：SH600519 / SZ000001 / BJ430047
- baostock socket 是否曾死：是 / 否
```

---

## 历史已知问题列表（v1.2.0-preview3 验收发现）

| ID | 描述 | 状态 |
|---|---|---|
| **B1** | `BaostockClient` TCP socket 死后无法自愈，登录态与实际 socket 状态脱节 | ✅ v1.2.0-preview5（Sprint 3 P0）已修 |
| B2 | `/api/loadtest/run` 对 baostock TCP 端点未拦截 concurrency>1 / heavy load | ✅ Sprint 2.5 批 3 已修 |
| M1 | `/api/multi/realtime-quotes` 空 codes 静默回退到默认股票 | ✅ Sprint 2.5 批 3 已修 |
| M2 | `/api/meta/endpoints` metadata 缺少 `protocol` 字段 | ✅ Sprint 2.5 批 3 已修 |
| M3 | 前端无 protocol 徽章 / 警告 banner / TCP concurrency 锁 | ✅ Sprint 2.5 批 3 已修 |
| N1 | metadata 默认日期硬编码 2024-xx-xx，跨年/跨季后陈旧 | ✅ Sprint 2.5 批 3 已修（每次 GET /api/meta/endpoints 重算） |
| N2..N7 | 其它低优体验问题 | 列入 v1.2.1 |
