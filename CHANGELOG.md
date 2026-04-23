# Changelog

All notable changes to this project will be documented in this file.

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
