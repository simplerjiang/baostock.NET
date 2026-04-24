# Baostock.NET.TestUI

交易员/开发者用的接口手测页面（Sprint 2.5 批次 2）。

## 启动

```powershell
cd src/Baostock.NET.TestUI
dotnet run
```

启动后浏览器访问 <http://localhost:5050>。

## 使用流程

顶部 Tab 切换：**API 调用** / **压测面板**。

### API 调用

1. 在顶部 Login 区输入账号密码（默认 `anonymous` / `123456`，匿名账号即可访问全部 baostock TCP API），点击 **Login**。
2. 左侧栏点开任意分组（session / history / metadata / sector / evaluation / corp / macro / special / multi）。
3. 选中一个 endpoint，右侧表单会按 metadata 自动渲染默认值。
4. 调整参数后点击 **Send**：
   - 顶部统计行显示 `ok / elapsedMs / rowCount / source`。
   - 下方 `<pre>` 展示完整响应 JSON（超过 200 行只显示前 200 行 + "查看全部"）。

### 压测面板

1. 切到 **压测面板** Tab。
2. 在 **Target endpoint** 下拉选择目标接口；选中后 Body 文本域会按 metadata 自动预填默认 JSON。
3. 调整 Body / Mode / Concurrency / Warmup 后点 **开始压测**：
   - Mode = `duration`：跑满 `Duration` 秒后停。
   - Mode = `count`：跑满 `TotalRequests` 次后停。
4. 后端同步执行，前端 `await fetch` 直接取整段统计结果（不需要轮询）。
5. 结果区展示 QPS / 错误率 / 总请求 / 平均延迟看板 + 延迟分位表（min/p50/p90/p95/p99/max/mean）+ 错误类型 Top 5 + 配置回显。

#### 安全限制

- `concurrency` ≤ 100（>100 返回 400）
- `durationSeconds` ≤ 300（5 分钟）
- `totalRequests` ≤ 100000
- `warmupRequests` 0..1000
- 同时只允许 1 个 loadtest 任务运行；并发提交第 2 个返回 `409 {ok:false, error:"another load test is running"}`
- 压测内部直接调用 endpoint handler delegate（不走 HttpClient/Kestrel 自调），避免连接放大与单进程压力假象。

#### ⚠️ 警告

- **任何 `/api/baostock/**` (baostock TCP) 端点都不建议 `concurrency > 1`**：单 `BaostockClient` 共享一条 TCP 长连接，协议解析非线程安全，并发 ≥2 会立刻引发 `IOException` 风暴（实测 Sprint 2.5 批次 2 验收）。该类端点的压测仅适合用于"串行延迟基线"测量。
- 真正支持高并发压测的只有 `/api/multi/*`（HTTP 聚合多源端点，每源独立 HttpClient + 健康感知）；其压测 QPS 还会受外部数据源（Sina/Tencent/EastMoney）限流影响。
- 不要对**全表端点**（如 `/api/baostock/metadata/all-stock`）跑任何并发，会拖死本机；该端点首次拉取约 11~12 秒（8000+ 行）。

## 注意事项

- **登录态保存在后端 `BaostockClient` 单例中**：浏览器多 tab 共用同一会话，不需要每个 tab 各自登录。
- 多源 endpoint（`/api/multi/*`）走 HTTP 抓取，**无需 baostock TCP Login**，不依赖 Login 状态。
- TestUI 项目仅供本地手测，未做鉴权/限流；不要暴露到公网。
- **请求体字段名大小写不敏感**：`adjustflag` / `adjustFlag` / `AdjustFlag` 都被识别（v1.2.0-preview3 批次 2 修复，原先精确匹配会静默走默认值）。

## 端点元数据

- `GET /api/meta/endpoints` 返回当前注册的全部 endpoint 描述（前端表单自动渲染依据）。
- `GET /api/loadtest/list-targets` 返回所有可压测目标（过滤掉 `/api/loadtest/*` / `/api/meta/*`），每条带 `defaultBody`。
- `POST /api/loadtest/run` 触发压测，请求/响应 schema 见 `Endpoints/LoadTestRunner.cs`。

