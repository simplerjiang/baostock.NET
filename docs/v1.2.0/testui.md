> v1.2.0 专集 | 面向从 v1.0.x 升级的用户 | 2026-04-24

# TestUI 子项目使用指南

Baostock.NET v1.2.0 引入 `Baostock.NET.TestUI` 子项目，是一个面向交易员、QA 与开发自己的极简 Web 前端 + minimal API 服务器，用于手动点测所有公开 API 与对其做小规模压测。

## 1. 定位

TestUI **不是**面向终端用户的产品 UI，也不是对外发布的交易终端。它的目标用户是两类人：交易员按端点列表手动逐项验收回归、以及开发者在写新功能 / 调 bug 时做冒烟与小规模压测。因此页面样式有意从简 —— 无主题切换、无图表、无交互动画，JSON 原样展示即为源真相。

## 2. 启动

```bash
cd c:\Users\kong\baostock.Net
dotnet run --project src/Baostock.NET.TestUI
```

然后在浏览器打开 `http://localhost:5050`。

## 3. 端点概览

TestUI 共暴露 **37 个** HTTP 端点，分为三大类：

| 分类 | 数量 | 子分组 |
|---|---|---|
| baostock TCP | 28 | History(2) / Metadata(3) / Sector(4) / Evaluation(8) / Corp(2) / Macro(5) / Special(4) |
| Multi-source HTTP | 3 | realtime-quote / realtime-quotes / history-k-line |
| Internal | 6 | login / logout / status / endpoints / list-targets / loadtest-run |

- **History(2)**：K 线日 / 分钟线
- **Metadata(3)**：股票基础信息 / 交易日 / 上市证券列表
- **Sector(4)**：行业分类 / 三种指数成分股
- **Evaluation(8)**：估值、财务表、杜邦、预告 / 快报等
- **Sector / Corp / Macro / Special** 细分见 `/api/meta/endpoints`

## 4. 协议徽章

页面上每个端点前挂一个小徽章，用于一眼识别其通信协议与并发特性：

- `[TCP]` — baostock 长连接。**线程安全约束：同一 `BaostockClient` 实例并发 ≥ 2 会触发 IOException 风暴。** 所有压测场景必须强制单并发，见第 5 节硬锁。
- `[HTTP]` — 多源 HTTP（新浪 / 腾讯 / 东方财富）。支持高并发，上限受源方限速约束，建议 `concurrency ≤ 20`。
- `[META]` — 内部运维端点，包含 `login` / `logout` / `status` / `endpoints` / `list-targets` / `loadtest-run`。不参与压测目标枚举。

## 5. 压测面板使用

压测入口由 `POST /api/loadtest/run` 暴露，前端提供表单封装。

**必填字段：**

- `targetPath`：压测目标端点路径（从 `/api/meta/list-targets` 拉取）
- `concurrency`：并发度
- `durationSeconds`：持续秒数
- `totalRequests`：总请求数上限（取 duration 与 total 先到者）

**硬限制**（后端强制，前端二次校验，二者不一致以后端为准）：

- **全局单实例**：同一时刻只允许 1 次 loadtest 运行；第二次提交返回 `HTTP 409 Conflict`。
- **全局上限**：`concurrency ≤ 100` / `duration ≤ 300s` / `total ≤ 100000`。
- **TCP 路径专属硬锁**：当 `targetPath` 命中 `/api/baostock/**` 时，后端强制降级为 `concurrency ≤ 1` / `duration ≤ 30s` / `total ≤ 200`（B2 单连接保护，防止触发 IOException 风暴）。

**预热**：前 N 次请求单线程串行跑完，**不计入**任何延迟 / 错误 / RowCount 指标。

## 6. 指标解读

loadtest 结果 JSON 包含以下字段：

- `p50 / p90 / p95 / p99`：采用 **nearest-rank 分位法**，公式 `sorted[ceiling(q * n) - 1]`，单位毫秒。
- `min / max / mean`：同样单位毫秒；`mean` 为算术平均。
- **错误 Top5**：按 `exception.GetType().Name` 聚合计数，仅展示前 5 种异常类型。超时归类为 `TimeoutException`，网络异常归类为 `IOException`。
- `rowCount`：
  - 对 multi-source HTTP 端点，为响应解析后的数据行数。
  - 对 baostock TCP 端点，为 baostock 结果集返回的 row 数。
  - 对 META 端点不计。

## 7. socket 健康指示灯（UX-a）

页面顶部固定一个小圆点，用于一眼判断 baostock TCP socket 健康状态：

- 绿色 `●` = socket 连接正常
- 红色 `●` = socket 未连接 / 半死

数据来源：`GET /api/session/status` 返回体的 `isSocketConnected` 字段。页面默认每 5 秒轮询一次。

**未登录时也会显示。** v1.2.0 正式版修正了一个认知偏差：logout 后 `isSocketConnected` 保持 true 是预期状态，因为 TCP socket 仍然打开，只是会话 token 被清除；完全断开需要显式 Dispose。

## 8. 典型使用流程

1. 启动 TestUI → 浏览器打开 `http://localhost:5050`
2. 右上角点 **Login**（默认匿名账号即可）→ 观察 socket 灯变绿
3. 左侧端点树中选择一个端点组 → 挑选一个具体端点
4. 填入参数（页面已为 `startDate` / `endDate` 等注入最近可用日期作为默认值）→ 点 **Run** → 右侧查看 JSON 结果与延迟
5. 想做压测？切到 **Loadtest** 标签 → 从下拉框选 `targetPath` → 设好并发 / 时长 / 总数 → **Run**，实时查看分位延迟曲线

## 9. 已知限制 / 不适合做的事

- 不是生产监控工具：**无持久化、无用户系统、无审计日志**。进程重启后所有运行记录丢失。
- **不支持 WebSocket / SSE**：当前所有端点都是请求 / 响应式。
- loadtest 指标只在本次运行窗口内显示，**无历史对比**；如需趋势分析请自行导出 JSON。
- 前端不做复杂 UI 交互（无分页、无排序、无搜索）——JSON 即源真相，肉眼检查或接到下游工具处理。
- TCP 端点在 loadtest 面板中 concurrency 被强制锁为 1，**不代表 baostock 只能单并发**，而是同一 `BaostockClient` 实例不能并发；多实例并发请直接用库，绕开 TestUI。

## 10. 相关文档

- [README.UserAgentTest.md](../../README.UserAgentTest.md) — 交易员验收手册（含 smoke test sequence）
- [架构](./architecture.md)
- [数据源](./sources.md)
- [迁移指南](./migration-from-1.0.md)
