# Changelog

All notable changes to this project will be documented in this file.

## v1.3.4 — 2026-04-25

### 修复 + 维护

- **416 响应 `Content-Range` 完全合规 RFC 7233 §4.2**(v1.3.3 已知瑕疵)
  - v1.3.3 中 416 响应 `Content-Range: bytes */*` 不符合规范；§4.2 要求 `unsatisfied-range = "*/" complete-length`
  - 修复：416 路径先拉上游全量流拿 total 字节数，再以 `bytes */<total>` 返回(即使 Range 无效也付出一次上游往返代价，确保规范合规)
  - 副效益：起始越界(如 `bytes=999999-`)现在能正确返 416 + total，v1.3.3 会盲目 200 透传
  - 副效益：end 越界(如 `bytes=0-999999`)现在按规范截断到 `total-1`

- **GitHub Actions 升级到 Node.js 24(应对 GitHub 2026-06-02 弃用)**
  - `actions/checkout` v4 → v6
  - `actions/setup-dotnet` v4 → v5
  - `actions/upload-artifact` v4 → v7
  - `actions/upload-pages-artifact` v3 → v5
  - `actions/deploy-pages` v4 → v5
  - 三个 workflow(ci.yml / docs.yml / release.yml)全部更新

- **`.gitignore` 增补 + 工作区清理**
  - 追加 `tmp_*` 等本地调试 artifact 规则
  - 清理 17 个未跟踪 tmp 文件

### 内部

- TestUI `pdf-download` 端点新增私有 `SkipExactAsync` 函数：因 `CninfoSource.ResponseOwnedStream` 不支持 Seek，206 切片通过 read-discard 跳过 startVal 字节再读取目标长度
- 416 响应路径补 stream dispose，避免 HttpResponseMessage 泄漏

### Breaking Changes

**无**。

### 测试

- 307 passed / 0 failed / 1 skipped(与 v1.3.3 基线一致)
- 0 warning / 0 error
- Test Agent 独立全量回归 PASS(8/8 Range 矩阵 + 5/5 端点回归)

## v1.3.3 — 2026-04-25

### 修复（v1.3.2 契约缺陷热修）

经 Test Agent 全量遍历发现 v1.3.2 三项 Minor feature 均存在契约缺陷，本版本一并修复。

- **Bug-1（HIGH）：`/api/meta/endpoints` 元数据中 3 个内部端点 method 字段错误**
  - 受影响端点：`/api/session/status`、`/api/meta/endpoints`、`/api/loadtest/list-targets`
  - v1.3.2 中 meta 声明 `method=POST`，但实际仅接受 GET，按 meta 调用返回 405
  - 修复：保留为 GET（符合 REST 语义），更正 `EndpointDescriptor.Method` 为 `"GET"`

- **Bug-2（HIGH）：`/api/multi/*` 三个对冲端点 envelope 缺失 `sources` 字段**
  - 受影响端点：`/api/multi/realtime-quote`、`/api/multi/realtime-quotes`、`/api/multi/history-k-line`
  - v1.3.2 中 `EndpointRegistry.BuildMulti` 三处 `RoutedEndpoint` 未传 `SourcesExtractor`
  - 修复：补齐 `SourcesExtractor`，三个端点 envelope 现正确返回 `sources: ["Sina"]` / `["EastMoney"]` / `["Tencent"]`

- **Bug-3（MEDIUM）：`/api/cninfo/pdf-download` Range 处理不合 RFC 7233**
  - v1.3.2 仅支持 `bytes=N-`（前缀偏移），`bytes=A-B`（端范围）被忽略，`bytes=-N`（后缀）行为未定义
  - 206 响应缺失 `Content-Range` 头；200 FULL 响应缺失 `Accept-Ranges: bytes` 头
  - 修复：
    - 完整支持 `bytes=A-` 与 `bytes=A-B` 两种格式
    - 不支持的格式（`bytes=-N` 后缀 / multi-range / 解析失败）→ `416 Range Not Satisfiable`
    - 206 响应必带 `Content-Range: bytes A-B/total`
    - 200 FULL 响应必带 `Accept-Ranges: bytes`
  - 6 案例矩阵实测全部符合规范

### 内部

- `Baostock.NET.Cninfo.CninfoSource.ResponseOwnedStream.Length` 在底层 stream 不支持 Length 时回落读取 HTTP `Content-Length` 响应头，确保 `Content-Range` 能给出确切 total
- 新增单测 `CninfoSourceRangeTests`（2 cases）：rangeStart=null 全量 + rangeStart=N 跳过偏移
- `README.UserAgentTest.md` 增加 v1.3.3 验收段 V3-1（meta 自洽）/V3-2（multi sources）/V3-3（PDF Range 6 案例）

### 已知瑕疵（非阻断，留 v1.3.4）

- 416 响应的 `Content-Range` 当前为 `bytes */*`，RFC 7233 §4.2 要求 `bytes */<complete-length>`

### Breaking Changes

**无**。

### 测试

- 307 passed / 0 failed / 1 skipped（v1.3.2 基线 305 + 新增 2 个 Range 单测）
- 0 warning / 0 error
- Test Agent 独立全量回归 PASS（不依赖 Dev 自验）

## v1.3.2 — 2026-04-25

### 优化（API 可观测性 + 用户体验）

- **响应顶层暴露 `sources` 字段**（Bug-N-02）：
  - HTTP 多源端点（财报三表 / 巨潮公告）的 `ApiResult` envelope 新增 `sources: string[]` 字段，列出实际返回数据的源名称（去重）。
  - 财报：`["EastMoney"]` 或 `["Sina"]`（Hedged 胜出方）
  - 巨潮：`["Cninfo"]`（单源一致性）
  - TCP 端点不受影响（`sources` 字段不出现在 JSON 中，`JsonIgnoreCondition.WhenWritingNull` 序列化）

- **TestUI PDF 下载支持 Range 请求**（Bug-N-04）：
  - `/api/cninfo/pdf-download` 端点现在解析 HTTP `Range: bytes=N-` 请求头，正确转发到 `BaostockClient.DownloadPdfAsync(adjunctUrl, rangeStart, ct)`
  - 带 Range 时返回 `206 Partial Content` + `Accept-Ranges: bytes`
  - 浏览器、`curl -C -`、`HttpClient` 续传场景全部生效

- **`/api/meta/endpoints` 元数据增加 `method` 字段**（Bug-N-05）：
  - `EndpointDescriptor` record 新增 `Method` 字段（默认 `"POST"`，所有当前注册端点均为 POST）
  - 前端可据此正确选择请求方法，不再依赖 trial-and-error

### 内部

- `EndpointRunner.ApiResult` 加 `Sources` 字段（可选，向后兼容）
- 通过反射从 row 提取 `Source` 属性（缓存 PropertyInfo），不强制源类共享接口
- `Range` 解析仅支持 `bytes=N-` 起始偏移格式（multi-range / suffix 暂不支持）

### Breaking Changes

**无**。所有新字段均为可选 + 默认值，旧调用方零改动可吃新 envelope。

### 测试

- 305 passed / 0 failed / 1 skipped（基线保持）
- 0 warning / 0 error

## v1.3.1 — 2026-04-24

### 修复

- **银行/券商利润表 `TotalOperateIncome` 字段兜底**（Finding B-ICBC）：
  - 上游东财和新浪对银行/券商公司的利润表响应原生不包含 `TOTAL_OPERATE_INCOME`/`BIZTOTINCO`（营业总收入），但都有 `OPERATE_INCOME`/`BIZINCO`（营业收入），在银行业务中两者语义等价。
  - v1.3.1 起,两个源 ParseResponse 在 `TotalOperateIncome == null && OperateIncome != null` 时自动从 `OperateIncome` 复制值。
  - 影响：工行 SH601398 / 建行 SH601939 等银行股的利润表 `totalOperateIncome` 现在返回非空数值（等于 `operateIncome`）。
  - 非银行股不受影响（原本就有独立 `TotalOperateIncome`，`??=` 不触发）。

### 文档

- `README.UserAgentTest.md` 模块 I 新增 **I6/I7/I8 硬性用例**：
  - I6：创业板 `SZ300750` AnnualReport（防 Bug-N-03 回归）
  - I7：科创板 `SH688981` AnnualReport
  - I8：北交所 `BJ430047` All
  - 失败均为 Blocker，不允许"以为该公司没发公告"的误判。

### 测试

- 新增 `Parse_BankTemplate_CopiesOperateIncomeToTotalOperate`（Sina + EastMoney 各一，共 +2）
- 测试基线：305 passed / 0 failed / 1 skipped（较 v1.3.0 +2）
- 构建：0 warning / 0 error

### Breaking Changes

**无**。完全兼容 v1.3.0 API。

## v1.3.0 — 2026-04-24

### 新增（HTTP 多源扩展）

- **财报三表全量查询**：`QueryFullBalanceSheetAsync` / `QueryFullIncomeStatementAsync` / `QueryFullCashFlowAsync`
  - Hedged（东财 P=0 + 新浪 P=1，500ms 对冲）
  - `FullBalanceSheetRow`（24 核心字段 + RawFields 兜底）
  - `FullIncomeStatementRow`（15 字段）
  - `FullCashFlowRow`（12 字段）
  - 支持按年度/季度/中报筛选（`FinancialReportDateType.ByReport | ByYear | BySingleQuarter`）

- **巨潮公告 + PDF 下载**：
  - `QueryAnnouncementsAsync(request)` — 支持分类（年报 / 半年报 / 季报 / 业绩预告 / 临时公告 / All）
  - `DownloadPdfAsync(adjunctUrl, rangeStart?)` — 返回 `Stream`，支持 `Range` 断点续传
  - `DownloadPdfToFileAsync(adjunctUrl, dest, resume)` — 直接落盘，支持 `resume` 断点续写
  - 源：单源（巨潮 `static.cninfo.com.cn`），`206 Partial Content` 协议

### TestUI 新增

- `/api/financial/balance-sheet` / `/api/financial/income-statement` / `/api/financial/cashflow`（POST）
- `/api/cninfo/announcements`（POST）+ `/api/cninfo/pdf-download`（GET 流式）
- Web 前端：`financial` / `cninfo` 两个新分组，公告查询成功后自动渲染 PDF 下载链接

### 内部

- `IFinancialStatementSource`（非泛型，3 方法）+ 私有 `FinancialStatementSourceAdapter<TRow>` 桥接 `IDataSource<,>` 喂给 `HedgedRequestRunner`
- `CninfoAnnouncementCategory` / `CninfoAnnouncementRequest` / `CninfoAnnouncementRow` / `ICninfoSource` / `CninfoSource`
- `CodeFormatter.CninfoOrgId` / `CninfoStock` 扩展（`gssh` / `gssz` / `gssb` 前缀）
- 测试：+50 单测（东财 9 / 新浪 9 / 巨潮 20 / Client 13，全离线），累计 **291 passed / 0 failed / 1 skipped**（Category!=Live）

### 已知限制

- 财报字段：不同公司类型（工 / 银 / 证 / 保）字段集差异较大，`RawFields` 保留原始 key/value 兜底
- 巨潮 PDF：极少数老公告 `adjunctUrl` 为空或 302 重定向，当前不自动 follow
- 无 Hedged 的 Cninfo：单源，注意健康监控（后续版本可能加备用源）
- 银行类股票（如工行 SH601398）利润表 `totalOperateIncome` 字段为空 — 上游 Sina 银行模板原生不含 `BIZTOTINCO`，`operateIncome`（对应 `BIZINCO`）仍填充。v1.3.1 将加自动复制。
- `README.UserAgentTest.md` 模块 I 暂未强制覆盖创业板 / 科创板 / 北交所硬性用例，v1.3.1 补齐。

## v1.2.0 — 2026-04-24

正式版发布。v1.2.0 系列累计包含：
- preview1: 基础设施（Http Hedged/SourceHealthRegistry/CodeFormatter）+ 27 baostock TCP API 入参东财风格统一（BREAKING）
- preview2: 实时行情三源（Sina/Tencent/EastMoney）+ 东财/腾讯双源历史 K 线
- preview3: TestUI 子项目（minimal API + 单页 HTML + 压测面板 + 31 endpoint）
- preview4: UR 第一轮验收热修（B2 前置防护 / M1 空 codes 拒绝 / M2 protocol 字段 / M3 前端动态警告 + TCP concurrency 锁 / N1 动态日期默认值）+ 新建 README.UserAgentTest.md
- preview5: B1 BaostockClient TCP 自愈（重连 + relogin + 凭据缓存 + CAS 线程安全）+ N-1~N-4 minor 修复
- **v1.2.0 正式版（本次）**: 前端 socket 健康状态灯（UX-a）+ 手册 logout 语义 + Part G 健康自检 + C3 文案 Contains 语义

### Accumulated BREAKING changes from v1.0.x → v1.2.0

1. 股票代码统一东方财富风格（SH600519/SZ000001/BJ430047）；CodeFormatter 自动翻译。
2. CodeFormatter 非法输入抛 ArgumentException（InnerException=FormatException）；CodeFormatter.Parse 仍抛 FormatException。
3. Models.*Row.Code 输出统一东财风格（无法识别时保留原始字符串）。
4. **半 BREAKING**：BaostockClient.IsLoggedIn 语义改为 `Session.IsLoggedIn && _transport.IsConnected`（以前只看 Session 对象）。

### Highlights

- 测试: 272 passed / 0 failed / 2 skipped（baseline +105 vs v1.0.x）
- Release build: 0 warning / 0 error（TreatWarningsAsErrors=true 全程保持）
- 三源 Hedged Requests 对冲架构（per-source CTS + winner-takes-all + health-aware 30s cooldown）
- Streaming Hedged Runner（首元素胜出语义，loser 自动取消）
- TestUI：31 endpoint 覆盖全 SDK + 压测面板（P50/P90/P95/P99 + nearest-rank 分位 + 错误 Top 5）
- BaostockClient TCP 自愈（半死检测 + 自动重连 + 凭据缓存，CAS 线程安全）
- UR 双轮验收（preview4 不通过 → preview5 PASS + 0 退化）

### Known Limitations (v1.2.1 追踪)

- 北交所 BJ 历史 K 线公网双源（EM/Tencent）均不可用（已用 tripwire 测试守护）
- BJ 实时报价在 Sina 全零时 fallback Tencent 但可能拿到 09:00 盘前价
- `_credentials` 内存明文（TODO v1.3.0 升级到 SecureString）
- `TcpTransport.IsConnected` 半死 Poll 分支的单元测试仅间接覆盖（Live 集成测试直接覆盖）

## v1.2.0-preview5 — 2026-04-24 (Sprint 3 P0)

### Fixed

- **B1 `BaostockClient` TCP 自愈（关键）**：socket 半死时自动重连 + relogin（最多重试 1 次，防死循环）。
  - `Protocol/ITransport.cs` 新增 `bool IsConnected { get; }` 契约。
  - `Protocol/TcpTransport.cs` 实现 `IsConnected`：基于 `socket.Connected` + `socket.Poll(0, SelectRead) && Available == 0` 半死检测；一次 `SendAsync`/`ReceiveFrameAsync` 抛 `IOException`/`SocketException` 后 `_isBroken` 锁定 true，后续 `IsConnected` 恒返回 false，直到 `ConnectAsync` 拆旧建新。`ConnectAsync` 幂等（已连接 no-op，broken 时 dispose 旧对象再新建 `TcpClient`）。
  - `Client/BaostockClient.cs` 的 `EnsureLoggedInAsync`（所有 query 入口）新增 B1 自愈分支：`_transport.IsConnected=false` 且有缓存凭据 → `ReconnectAndReloginAsync`（内部用 `_reconnectInProgress` 守门防递归）。`LoginAsync` 成功后缓存 `_credentials=(userId,password)` 到私有字段（TODO v1.3.0：改 `SecureString`）。
  - **半 BREAKING**：`BaostockClient.IsLoggedIn` 属性语义变更——现为 `Session.IsLoggedIn && _transport.IsConnected`。之前只读 `Session.IsLoggedIn`，socket 半死时会误报 true。`Session.IsLoggedIn`（原 `BaostockSession` 属性）语义不变，仍是内存登录态。
- **N-1 `/api/baostock/evaluation/dividend-data` 默认 year**：`EndpointRegistry.cs` 从硬编码改为 `DateTime.Today.Year - 1`（上一完整年度，避免落到本年导致返回 0 行）。`performance-express-report` 等已用 `startDate`/`endDate` 范围型默认（今天前 30 天~今天），无需改动。
- **N-2 TestUI loadtest 时间字段本地化**：`wwwroot/app.js` 渲染 `startedAt`/`endedAt` 时用 `new Date(utc).toLocaleString('zh-CN')` + `(本地)` 标签；原始 UTC ISO 串保留在 `title` 属性，hover 可见。
- **N-3 TestUI internal endpoints 出现在 `/api/meta/endpoints`**：原 `EndpointRegistry.BuildInternalMeta()` 的 dead code 改为暴露给 `/api/meta/endpoints`（`protocol="internal"`），激活前端早已实现的 `[META]` 徽章 + internal banner + 压测按钮禁用 3 条分支逻辑。

### Added

- **`BaostockClient.IsConnected`** 公开属性：暴露底层 `ITransport.IsConnected` 给上层 UI / `/api/session/status`，避免 UI 依赖私有字段。
- **`/api/session/status`** 响应新增 `isSocketConnected` 字段（值取自 `BaostockClient.IsConnected`）。配合 `isLoggedIn` 可清晰区分"会话登录态"与"底层 socket 健康"两个维度。
- 测试：`TransportHealthTests`（`IsConnected` 在未连接/dispose 后均为 false）+ `BaostockClientReconnectTests`（socket broken 后自动 relogin；reconnect 失败抛 `BaostockException`；无凭据时不自动 login）。共新增 5 测试，总数 267 → 272。

### Doc

- **N-4 `README.UserAgentTest.md`** 已知约束追加 **D. PowerShell 直接调用 baostock 端点须显式指定日期范围**（实测 trade-dates 不传日期返回 4132 行的现象）；并在 C 节补注 v1.2.0-preview5 TCP 自愈上线。

### Notes

- Sprint 2.5 遗留的 B1 已清。**Sprint 3 主体（财报三表 + 巨潮 PDF）不在本批**，下一批排入。
- 约束遵守：不引入新 NuGet；0 warning 0 error；`IsConnected` 查询 O(1)；reconnect 硬上限 1 次重试；`_credentials` 明文（TODO v1.3.0 `SecureString`）。

## v1.2.0-preview4 — 2026-04-24 (Sprint 2.5 批 3)

### Added

- **`README.UserAgentTest.md`**（仓库根）：User Representative Agent / 真实交易员人工复测手册，覆盖 v1.2.0 已交付能力。含启动准备、3 条已知约束（baostock TCP 不可并发 / BJ 数据陈旧 / 登录态-socket 脱节）、6 个测试模块（A-F）、失败分流表、改进建议提交格式、历史已知问题清单。
- **`EndpointDescriptor.Protocol`** 字段（`Endpoints/EndpointRunner.cs`，默认 `"tcp"`）：`/api/meta/endpoints` 与 `/api/loadtest/list-targets` 响应均新增 `protocol` 字段。`/api/baostock/*` = `"tcp"`，`/api/multi/*` = `"http"`。前端据此渲染色块徽章 + 警告 banner。
- 前端：API 调用 Tab 左侧 endpoint 列表加 `[TCP]/[HTTP]/[META]` 徽章；详情区顶部加 protocol-aware banner（TCP 黄色警告、internal 灰色提示）。
- 前端：压测面板切换 Target 时根据 protocol 动态调整输入限制 —— TCP 端点强锁 `concurrency=1`、`totalRequests=50/max 200`、`duration max=30s` + 黄色 banner；http 端点恢复满量程；internal 端点禁用「开始压测」按钮。提交前 onsubmit 校验 TCP+concurrency>1 直接 alert 拦截。

### Fixed

- **B2 `/api/loadtest/run` baostock TCP 入口防护（关键）**：`Program.cs` 在已有 `endpointLookup` 命中后加双层后端校验。targetPath 命中 `/api/baostock/*` 时：
  - `concurrency > 1` → HTTP 400 `"baostock TCP endpoint does not support concurrency > 1 (single shared TCP connection, non-thread-safe). Use concurrency=1 for serial latency baseline only."`
  - `concurrency = 1` 但 `totalRequests > 200` 或 `durationSeconds > 30` → HTTP 400 `"baostock TCP endpoint heavy load (>200 requests or >30s duration) is discouraged; consider using /api/multi/* hedged endpoints instead."`
  防止用户跑 1000-请求/5-分钟 串行把 baostock 上游搞烦。后端校验是必须的，前端只是体验提升。
- **M1 `/api/multi/realtime-quotes` 空 codes 防护**：`EndpointRegistry.cs` 移除原 `if (codes.Length == 0) codes = new[] { "SH600519", "SZ000001" };` 静默回退；改为抛 `ArgumentException("codes is required and must be non-empty")` → `EndpointRunner.RunAsync` 自动映射为 `ApiResult { ok=false, error=..., errorType="ArgumentException" }`。修复前空 body POST `/api/multi/realtime-quotes` 会回退到默认股票并返回 ok=true（产品级错误：用户拿到不是自己请求的数据还以为是）。
- **M2 metadata 缺 `protocol` 字段**：见上方 Added。
- **M3 前端缺 protocol 警告 + concurrency 强制限制**：见上方 Added。
- **N1 metadata 默认日期动态化**：`EndpointRegistry.cs` 引入 `Today() / DaysAgo(int) / CurrentYear() / LastCompletedQuarter()` helper，所有原 `"2020-01-01" / "2024-01-01" / "2024-12-01" / "2024-12-31" / "2024-01-31" / "2023" / "4"` 硬编码替换为动态求值。`Program.cs` `/api/meta/endpoints` 改为每次请求重新 `EndpointRegistry.BuildAll().Select(e => e.Meta).ToList()`，保证进程跑过夜后 metadata 默认日期跟着自然日滚动。压测路由仍走启动期捕获的 `endpointLookup`，handler 引用稳定。

### Notes

- Sprint 2.5 批 3 验收：267/0/2，0 warning 0 error；黑盒自验 5/5 通过（meta 含 28 tcp + 3 http；baostock concurrency=2 → 400；baostock concurrency=1 totalRequests=300 → 400；空 codes → ok:false；meta 默认 startDate 含今年）。
- **B1（BaostockClient TCP 自愈）未修，登记为 Sprint 3 P0 起手项**。本批为 UI/防护层快修，不动 SDK 协议层。

## v1.2.0-preview3 — 2026-04-24

### Added

- **`Baostock.NET.TestUI`** 新项目（`src/Baostock.NET.TestUI/`）：交易员/开发者用的接口手测页面。
  - ASP.NET Core minimal API（`Microsoft.NET.Sdk.Web`，监听 `http://localhost:5050`），`BaostockClient` 注册为单例，登录态在浏览器多 tab 间共享。
  - 覆盖**全部 28 个 baostock TCP query API**（history×2 / metadata×3 / sector×4 / evaluation×8 / corp×2 / macro×5 / special×4）+ Login/Logout/Status，分组路由按 `/api/baostock/{group}/{kebab-case-name}` 命名。
  - 覆盖 **3 个多源 API**（`GetRealtimeQuoteAsync` / `GetRealtimeQuotesAsync` / `GetHistoryKLineAsync`），路径 `/api/multi/*`，响应保留 `source` 字段揭示胜出源。
  - 统一响应包装 `{ ok, elapsedMs, rowCount, data, raw }`，异常自动映射为 `{ ok:false, error, errorType }`。
  - `GET /api/meta/endpoints` 返回手工维护的 endpoint metadata（`Endpoints/EndpointRegistry.cs`），驱动前端表单自动渲染（含 enum → `<select>`）。
  - 0 依赖单页前端（`wwwroot/index.html` + `app.js` + `style.css`）：左侧分组列表 / 右侧自动表单 + JSON 折叠展示。
  - 不引入任何新 NuGet / 前端框架；`TreatWarningsAsErrors=true` 与主项目对齐。
  - 启动：`dotnet run --project src/Baostock.NET.TestUI`，浏览器访问 <http://localhost:5050>。
  - 注：压测面板留待 Sprint 2.5 批次 2。

### Added (Sprint 2.5 批次 2)

- **TestUI 压测面板**：顶部新增 Tab 切换（API 调用 / 压测面板）；压测表单含目标 endpoint 下拉、Body JSON、Mode（duration/count）、Concurrency、Warmup；结果区展示 QPS / 错误率 / 延迟分位（min/p50/p90/p95/p99/max/mean）/ 错误类型 Top 5 / 配置回显。无新引入 NuGet / 前端图表库。
- **`POST /api/loadtest/run`**：后端进程内压测，直接调用 endpoint handler delegate（不走 HttpClient/Kestrel 自调）。`worker = Task.Run × concurrency`，duration 模式按 `Stopwatch` 跨界终止；count 模式用 `Interlocked.Decrement` 抢任务；延迟以 `Stopwatch.GetTimestamp()` 高精度收集到 `ConcurrentBag<double>`，分位数 nearest-rank（`sorted[(int)Math.Ceiling(q*n) - 1]`）；异常按 `GetType().Name` 聚合到 Top 5；warmup 单线程预热不计统计。完整 `CancellationToken` 贯穿到每个 worker。
- **`GET /api/loadtest/list-targets`**：返回除 `/api/loadtest/*` / `/api/meta/*` 之外的全部可压测 endpoint，每条携带 `defaultBody` 给前端预填。
- **压测安全限制**：`concurrency ≤ 100` / `durationSeconds ≤ 300` / `totalRequests ≤ 100000` / `warmupRequests 0..1000`；同时只允许 1 个任务（`SemaphoreSlim(1,1)` 非阻塞抢占，第 2 个返回 HTTP 409）。

### Fixed (Sprint 2.5 批次 2)

- **TestUI endpoint body 字段名现支持大小写不敏感**：`Endpoints/EndpointRunner.cs` 新增 `TryGetPropertyCI`，`GetString` / `GetInt` / `GetEnum` / `GetStringArray` 均改走 CI 路径。修复前 `adjustflag`（全小写）调 `/api/baostock/history/k-data-plus` 会被静默忽略走 `PreAdjust` 默认值；修复后 `PostAdjust` 正确生效（实测 SH600519 2024-12-02 复权后开盘价 `10848.38`，与 PreAdjust 默认下的 `~1500` 区段截然不同）。

## v1.2.0-preview2 — 2026-04-24

### Added

- **`Baostock.NET.Models.RealtimeQuote`**：多源对冲后的实时行情快照 record。量纲统一（成交量=股、成交额=元、价格保留原始精度），时间戳=北京时间 `Unspecified`。
- **`BaostockClient.GetRealtimeQuoteAsync(string code, ...)` / `GetRealtimeQuotesAsync(IEnumerable<string> codes, ...)`**：单只 / 批量实时行情，三源对冲调度（Sina(0) → Tencent(1) → EastMoney(2)），hedge 间隔 500ms。
- **`Baostock.NET.Realtime.SinaRealtimeSource`**：新浪 `hq.sinajs.cn`，GBK 解码，必带 `Referer: https://finance.sina.com.cn`；payload 全零（典型场景：BJ 停牌 / 集合竞价前）自动抛 EMPTY 让 hedge fallback。
- **`Baostock.NET.Realtime.TencentRealtimeSource`**：腾讯 `qt.gtimg.cn`，GBK 解码，成交量从「手」×100 归一化为「股」，成交额从「万元」×10000 归一化为「元」。
- **`Baostock.NET.Realtime.EastMoneyRealtimeSource`**：东财 `push2.eastmoney.com/api/qt/stock/get`，按 `f152` 反归一化价格，必带 `Referer` + `ut` token；BJ secid 临时按 `116.{code}` 输出（社区惯例，未线上 100% 验证，集成测试通过 hedge fallback 兼容）。
- **`Baostock.NET.Models.EastMoneyKLineRow`**：独立的多源历史 K 线行 record，与 baostock 协议的 `Models.KLineRow` 解耦（字段集差异：含 `Amplitude` / `ChangePercent` / `ChangeAmount` / `TurnoverRate`，无 `PreClose` / `PctChg` 等 baostock 专属字段；`TurnoverRate` 在 Tencent 源下为 `null`）。
- **`Baostock.NET.KLine.KLineRequest`** + 内部 `KLineFrequencyMapping`：双源历史 K 线请求模型 + 频率/复权枚举到 EM/Tencent URL 参数的映射表。
- **`Baostock.NET.KLine.EastMoneyKLineSource`** (Priority=0)：东财 `push2his.eastmoney.com/api/qt/stock/kline/get`，11 字段 csv 解析（`f51..f61`），`vol` 单位由「手」×100 归一化为「股」，支持 5/15/30/60 分钟 + 日/周/月 + 不/前/后复权。
- **`Baostock.NET.KLine.TencentKLineSource`** (Priority=1)：腾讯 `web.ifzq.gtimg.cn/appstock/app/{fqkline,kline,mkline}`，三个 endpoint 自动按频率/复权选择；**字段顺序严格 `[date, open, close, high, low, volume]`**（close 在 high 之前，专门加 trap 测试覆盖）；`Amplitude/ChangePercent/...` 字段 Tencent 不返回，置 `null`。
- **`BaostockClient.GetHistoryKLineAsync(code, frequency, startDate, endDate, adjust=PreAdjust, ct)`**：双源对冲历史 K 线，返回 `IReadOnlyList<EastMoneyKLineRow>`；hedge 间隔 500ms，全部失败抛 `AllSourcesFailedException`。
- **`Baostock.NET.Util.EastMoneySecIdResolver`**：从 `EastMoneyRealtimeSource.ResolveSecId` 抽出的共享 EM secid 翻译器（SH=`1.{c}` / SZ=`0.{c}` / BJ=`116.{c}`），realtime + kline 两个 EM 源复用，避免 BJ TODO 重复散落。
- `HttpDataClient` 静态构造内集中注册 `CodePagesEncodingProvider`，后续所有 GBK / GB18030 接口直接调用 `Encoding.GetEncoding("GB18030")`。

### Notes

- v1.2.0 Sprint 2 Phase 1：实时行情三源对冲首发（Sina+Tencent+EastMoney），共 +16 测试（每源 3 + Multi 4 + Integration 3，含 SH600519 / SZ000001 / BJ430047）。
- v1.2.0 Sprint 2 Phase 2：历史 K 线双源对冲（EM+Tencent），共 +18 测试（EM 4 + Tencent 6 + Multi 4 + Integration 4）。Tencent 字段顺序 trap 测试以 SH600519 2026-03-13 真实样本（`open=1392.48 / close=1413.64 / high=1417.62`，close < high）证明字段顺序未颠倒。
- BJ430047 (诺思兰德) 实时实测：Sina 返回 all-zero（已识别为 EMPTY 抛出）→ 成功 fallback 到 Tencent；EM secid 116 偶发 ResponseEnded（连接被服务端中断），TODO 在 Sprint 3 拉到稳定 BJ fixture 后再固化前缀。
- **BJ K 线已知限制（Sprint 2 Phase 2 实测，2026-04-24）**：腾讯 `fqkline/get` 与 `kline/kline` 对所有 BJ 代码（bj430047/bj430139/bj873169/bj430718/bj836395 等 10+ 已测）`data.{code}.day` 数组始终为空 `[]`；东财 `push2his` 在 `secid=116.{code}` 上 ResponseEnded（其它前缀 81/82/90/8/128/152 也均不通）。两源对 BJ 历史 K 线均不可用，目前 `GetHistoryKLineAsync("BJ...")` 会抛 `AllSourcesFailedException`。集成测试 `GetHistoryKLine_BJ430047_Daily_Last30Days` 已断言这一现状作为 tripwire（任一源恢复后该用例会失败，提示更新实现）。Sprint 3 计划接入 AKShare/CNINFO 等替代源补齐 BJ K 线。
- 所有 fixture 来自 `tools/source-probes/raw/` Sprint 0 实测样本，未编造任何响应。

## v1.2.0 — 2026-04-24

### ⚠️ BREAKING

- **股票代码统一为东方财富风格**（`SH600519` / `SZ000001` / `BJ430047`），SDK 内部 `CodeFormatter` 翻译为 baostock 协议格式 `sh.600519`。
  - 影响：所有接受 `string code` 入参的 27 个 API
  - 兼容：CodeFormatter 同时接受 `sh.600519` / `sh600519` / `600519.SH` / `1.600519`(secid)，宽容输入
  - 升级方式：现有调用代码可不改（旧格式仍接受），但建议改成东财风格以保持一致
- **入参非法时抛 `ArgumentException`**（`InnerException` = `FormatException`），与 v1.0.x 行为一致；`CodeFormatter.Parse` 直接调用仍抛 `FormatException`。受影响便捷方法：`CodeFormatter.ToBaostock` / `ToEastMoney` / `ToSecId` / `ToCninfoStock`。
- **`Models.*Row.Code` 字段输出格式改为东方财富风格**（如 `SH600519`），与入参格式一致；服务器原始 baostock 格式（`sh.600519`）不再外露。无法识别的特殊代码（极少数 ETF/指数）保留服务器原始字符串。

### Added

- `Baostock.NET.Http` 命名空间：HedgedRequestRunner / StreamingHedgedRunner / SourceHealthRegistry / RetryPolicy / HttpDataClient / IDataSource / DataSourceException
- `Baostock.NET.Util.CodeFormatter`：股票代码格式互转（东财↔baostock↔腾讯/新浪↔东财 secid↔巨潮 stock）

### Fixed

- HttpDataClient 便捷方法 `GetStringAsync` / `PostFormAsync` 的 timeout 现在覆盖 header + body 全过程

### Notes

- v1.2.0 仅交付基础设施 + 现有 27 API 入参规整化；多源行情/财报/PDF 实际接入留待 Sprint 2~5
- 实现细节：6 个季频财务方法（QueryProfit/Operation/Growth/Dupont/Balance/CashFlowDataAsync）走私有辅助 `QuerySeasonAsync<T>`，入参翻译集中在该辅助中，避免在 6 个表达式方法内重复插入。
- `QueryStockBasicAsync` / `QueryStockIndustryAsync` 中 `code` 是可选参数；空值保持“不按代码筛选”语义，仅在非空时才调用 `CodeFormatter.ToBaostock`。
- `Client.History.cs` 中原有“code 长度必须为 9”的预检查已移除（CodeFormatter.ToBaostock 已在输入不合法时招 FormatException）。
- Models 中 `Code` 字段统一反向格式化为东方财富风格（`SH600519`），与入参一致；解析层使用 `CodeFormatter.TryParse` 降级，无法识别的特殊代码保留服务器原始字符串。

## v1.0.1 — 2026-04-23

### Fixed

- **KLine 解析 IndexOutOfRangeException**：`ParseKLineRow` 和 `ParseMinuteKLineRow` 在服务器返回列数不足时抛出 `IndexOutOfRangeException`，现已通过 `SafeCol` 辅助方法添加边界保护，缺失字段回退为默认值（`null` / `NoAdjust` / `Suspended` / `false`）。

### Added

- 8 个边界场景单元测试覆盖 `ParseKLineRow` / `ParseMinuteKLineRow` 的列数不足、空字段、完整列等情况。
- `InternalsVisibleTo` 支持，允许测试项目直接测试内部解析方法。

## [1.0.0] - 2026-04-23

### Added
- 完整复刻 baostock Python 客户端全部 27 个公开 API
- 日频 K 线数据 (`QueryHistoryKDataPlusAsync`)
- 分钟频 K 线数据 (`QueryHistoryKDataPlusMinuteAsync`) — 5/15/30/60 分钟
- 板块数据：行业分类、沪深300/上证50/中证500 成分股
- 季频财务：盈利/营运/成长/杜邦/偿债/现金流/除权除息/复权因子
- 公司公告：业绩快报、业绩预告
- 元数据：交易日查询、全部证券、证券基本资料
- 宏观经济：存贷款利率、存款准备金率、货币供应量
- 特殊股票：退市/暂停/ST/*ST
- 纯 .NET 9 TCP 协议实现，无 Python 依赖
- async/await 全异步，IAsyncEnumerable<T> 流式返回
- 强类型 record 模型（KLineRow, ProfitRow 等 20+ 类型）
- 完整中文 API 文档（30 页）
- DocFX 文档站 + GitHub Pages 部署
- GitHub Actions CI（build + test）
- NuGet 包 `Baostock.NET`

### Protocol
- 服务端 BodyLength 字段对中文响应按字符数填写（非字节数），已正确处理
- CRC32 为变长十进制 ASCII（不补零），与 Python zlib.crc32 输出一致
- 压缩响应（MSG=96）走 zlib 解压
- 错误帧（MSG=04）正确映射为 BaostockException

## [0.9.0] - 2026-04-23

### Added
- 全部 24 个 query API 实现
- NuGet 包元数据
- GitHub Actions CI
- README API 对照表

## [0.5.0] - 2026-04-23

### Added
- 宏观经济 5 个 query
- 公司公告 2 个 query
- 特殊股票 4 个 query

## [0.4.0] - 2026-04-23

### Added
- 板块 4 + 元数据 3 + 季频财务 8 = 15 个 query
- ResponseParser 通用响应解析

## [0.3.0] - 2026-04-23

### Added
- QueryHistoryKDataPlusAsync (K线日频)
- KLineRow 强类型模型
- AutoLogin 机制

## [0.2.0] - 2026-04-23

### Added
- TcpTransport + ITransport
- LoginAsync / LogoutAsync
- BaostockException
- xUnit live test serialization

### Fixed
- 服务端 BodyLength 字符数 vs 字节数问题

## [0.1.0] - 2026-04-23

### Added
- Constants (MSG types, error codes, server config)
- MessageHeader encode/decode
- FrameCodec (framing, CRC32, zlib)
- 16 个协议层单元测试
- Golden fixtures (25 APIs)
