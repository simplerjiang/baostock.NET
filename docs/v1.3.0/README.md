> v1.3.0 专集 | HTTP 多源扩展：财报三表 + 巨潮公告 / PDF | 2026-04-24

# Baostock.NET v1.3.0 专集

v1.3.0 在 v1.2.0 的 Hedged Requests 基础设施之上，横向扩展了两类 HTTP 数据能力：**财报三表全量查询** 与 **巨潮资讯网公告检索 / PDF 下载**。**零 BREAKING CHANGES**，v1.2.0 项目可直接升级。

## 亮点

- **财报三表（东财 + 新浪双源对冲）**：资产负债表 / 利润表 / 现金流量表的单期完整字段。东财为 P=0 主源，新浪为 P=1 备用源，沿用 v1.2.0 的 `HedgedRequestRunner`（500ms hedge 间隔 + 30s 健康冷却）。
- **巨潮公告 + PDF 下载**：按分类（年报 / 半年报 / 季报 / 业绩预告 / 临时公告）检索公告索引，流式 PDF 下载支持 `Range` 断点续传。
- **TestUI 新增 5 个端点**：3 个财报 + 1 个公告查询 + 1 个 PDF 流式下载，前端公告查询成功后自动渲染下载链接。
- **字段兜底**：财报字段随公司类型（一般 / 银行 / 证券 / 保险）差异显著，`FullXxxRow.RawFields` 保留原始 key/value 字典，调用方可按需提取未建模字段。

## 子文档导航

- [财报三表详解](./financial-statements.md) — 3 个 API 签名 / `FullBalanceSheetRow` / `FullIncomeStatementRow` / `FullCashFlowRow` 字段表 / 东财 vs 新浪字段映射 / Hedged 机制
- [巨潮公告 + PDF](./cninfo-pdf.md) — 分类枚举映射 / Host 拆分 / `Range` 断点续传协议 / Accept-Encoding 规避
- [TestUI 新端点](./testui.md) — 5 个新端点的请求 / 响应示例与前端使用流程
- [从 v1.2.0 迁移](./migration-from-1.2.md) — 零 BREAKING 声明 + 新增 public API 清单 + 最少 5 行启用代码

## 测试与质量

- **291 passed / 0 failed / 1 skipped**（`Category!=Live`，v1.2.0 基线 272 → v1.3.0 累计 291，新增 ≈ 50 测试但部分与既有文件合并）
- **0 warning / 0 error**，`TreatWarningsAsErrors=true` 全程保持
- 覆盖：东财财报源单测 9 / 新浪财报源单测 9 / 巨潮源单测 20 / `BaostockClient` 客户端扩展单测 13，全部离线（`HttpMessageHandler` mock）

## 已知限制

- **财报公司类型差异**：银行 / 证券 / 保险的财报结构与一般工商业差异极大，`FullBalanceSheetRow` / `FullIncomeStatementRow` / `FullCashFlowRow` 建模的是一般工商业核心字段，其它类型请优先访问 `RawFields`。
- **巨潮单源**：`QueryAnnouncementsAsync` / `DownloadPdfAsync` 目前只有巨潮一个源，不走 Hedged；健康统计已记录在 `SourceHealthRegistry`（source 名 `"Cninfo"`），后续版本可能并入备用源。
- **巨潮 PDF 302 / 空 adjunctUrl**：极少数老公告 `adjunctUrl` 为空字符串或返回 302 重定向，当前版本不自动 follow；调用方需自行过滤 `string.IsNullOrWhiteSpace(adjunctUrl)` 行。
- **Accept-Encoding 排除 brotli**：巨潮 PDF 下载故意不声明 `br`，避免 CDN 返回被压 PDF 导致读流失败（详见 [cninfo-pdf.md](./cninfo-pdf.md#4-accept-encoding-规避)）。

## 相关文档

- [CHANGELOG.md](../../CHANGELOG.md) — 完整版本变更记录（顶部 `[Unreleased] - v1.3.0` 段）
- [README.UserAgentTest.md](../../README.UserAgentTest.md) — 交易员黑盒测试手册（模块 H / 模块 I 对应 v1.3.0 新能力）
- [v1.2.0 专集](../v1.2.0/README.md) — v1.2.0 架构 / 数据源 / TestUI 基础
- [主 README](../../README.md) — 项目总览与快速上手
