> v1.2.0 专集 | 面向从 v1.0.x 升级的用户 | 2026-04-24

# Baostock.NET v1.2.0 专集

v1.2.0 把项目从单源 baostock TCP 协议封装升级为 **A 股综合数据 SDK**，在保留原 baostock 能力的基础上引入 **Hedged Requests + 健康感知多源架构**，为实时行情与历史 K 线提供多源互备的高可用数据通道。

## 亮点

- **三源实时行情**：Sina / Tencent / EastMoney 并发对冲，500 ms hedge 间隔，首个成功胜出
- **双源历史 K 线**：EastMoney 主源（11 字段全），Tencent 备用源（6 字段）
- **统一证券代码格式**：采用东财风格 `SH600519`（大写、无点），`CodeFormatter` 向后兼容旧格式
- **TCP 自愈**:半死检测 + 自动 reconnect + 自动 relogin,CAS 锁保证线程安全
- **TestUI 子项目**:37 个 endpoint + 压测面板,方便交易员与 QA 逐项验收
- **4 条 BREAKING CHANGES**:证券代码格式 / 异常类型契约 / Models 输出格式 / `IsLoggedIn` 语义(详见迁移指南)

## 子文档导航

- [架构](./architecture.md) — Hedged Requests、SourceHealthRegistry、HttpDataClient、RetryPolicy、TCP 自愈
- [数据源](./sources.md) — 三方源返回字段 / 限制 / 对比表
- [从 v1.0.x 迁移](./migration-from-1.0.md) — BREAKING 逐项前后对比 + 代码迁移
- [TestUI 使用](./testui.md) — 启动、端点、压测约束

## 测试与质量

- **272 passed / 0 failed / 2 skipped**
- **0 warning / 0 error**,构建与测试双零
- 覆盖单元测试、集成测试(需 baostock 生产环境连通)、TestUI 黑盒测试

## 已知限制

- **BJ 历史 K 线公网双源瘫痪**:EastMoney `116.` 端点 `ResponseEnded` + Tencent 返回空数组,`AllSourcesFailedException` 无法规避
- **BJ 实时盘前陈旧**:09:00 之前 Sina 全零、Tencent 缓存昨收;开盘后恢复正常
- **credentials 内存明文**:`BaostockClient` 为支持自动 relogin 在内存缓存 `(UserId, Password)`,v1.3.0 将升级为 `SecureString`

## 相关文档

- [CHANGELOG.md](../../CHANGELOG.md) — 完整版本变更记录
- [README.UserAgentTest.md](../../README.UserAgentTest.md) — 交易员黑盒测试手册
- [主 README](../../README.md) — 项目总览与快速上手
