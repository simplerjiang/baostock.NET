# Baostock.NET v1.3.4 专集

发布日期：2026-04-25  
NuGet：[Baostock.NET 1.3.4](https://www.nuget.org/packages/Baostock.NET/1.3.4)  
基于 commit `2e2afb4`

## 概述

v1.3.4 是维护版本，无功能新增，无 Breaking Changes。修复 v1.3.3 已知瑕疵 + GitHub Actions Node.js 24 升级 + 工作区维护。完整 changelog 见 [CHANGELOG.md](../../CHANGELOG.md)。

## 文档

- **[端点状态快照](endpoint-snapshot.md)** — 41 个 TestUI 端点的全量实测报告（参数 / 响应 / 性能 / 注意事项）
- **[数据正确性交叉验证报告](data-correctness-cross-validation.md)** — 16 个端点与东方财富 / 巨潮 / 央行 / 统计局等权威源对比（12 MATCH + 4 DEVIATION 已记录口径差异 + 0 MISMATCH）
- [CHANGELOG v1.3.4 段](../../CHANGELOG.md) — 修复明细

## 升级

```xml
<PackageReference Include="Baostock.NET" Version="1.3.4" />
```

零 BREAKING，从 v1.3.0+ 任意版本可直升。

## 历史版本

- [v1.3.0 专集](../v1.3.0/README.md) — 财报三表 + 巨潮 PDF
- [v1.2.0 专集](../v1.2.0/README.md) — 多源对冲架构 + TestUI 子项目
