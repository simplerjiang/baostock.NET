# Baostock.NET 总体计划书

> 本文是 Baostock.NET 这一独立开源项目的工程总纲，所有 API 清单与协议常量均以 `reference/baostock-python/`（baostock 官方 Python 客户端 `00.9.10` 源码快照）为唯一事实来源。任何与本计划书冲突的口头描述都以源码为准。

---

## 1. 项目目标

**一句话定位**：Baostock.NET 是 baostock 官方 Python 客户端（PyPI 包 `baostock`，参考版本 `00.9.10`，常量定义于 `reference/baostock-python/common/contants.py` 的 `BAOSTOCK_CLIENT_VERSION`）的纯 .NET 9 复刻实现。

**三条硬目标**：

1. **完美翻译（API parity）**：公开 API 1:1 覆盖 Python 包通过 `reference/baostock-python/__init__.py` 导出的全部 `query_*` / `login` / `logout` / `set_API_key` 方法；命名以 PascalCase + `Async` 后缀重映射，语义不变。
2. **完全可用（wire-compatible）**：与 `public-api.baostock.com:10030` 的真实 TCP 服务端联通；同一参数下，每个 `query_*` 返回的字段名、字段顺序、字段值与 Python 客户端逐字段对齐（采用 §7 黄金对比测试验收）。
3. **可发布（shippable）**：GitHub 开源（MIT）、NuGet 正式包（`Baostock.NET`）、DocFX 静态文档站（GitHub Pages）。

**非目标（明确不做）**：

- 不做本地数据缓存层 / 持久化存储
- 不做实时行情推送、订阅
- 不做策略回测、指标计算、绘图
- 不做图形界面
- 不复制 Python 客户端源码到 NuGet 产物中（`reference/` 仅用于研究协议）

---

## 2. 上游 API 全量清单

下表全部基于 `reference/baostock-python/` 实际源码 grep 校验：

- "导出列" = 是否在 `reference/baostock-python/__init__.py` 中被 `from ... import` 导出（"是" = v1.0 必须实现；"否" = 候选 API，归入 §6 v0.5+ 视情况实现）
- "MSG 请求/响应" = 在源码中 `data.msg_type = cons.MESSAGE_TYPE_*` 实际使用的请求码 / 响应码（来自 `common/contants.py`）

### 2.1 登录登出（`reference/baostock-python/login/loginout.py`）

| Python 函数 | 导出 | MSG 请求 | MSG 响应 | C# 目标方法 |
| --- | --- | --- | --- | --- |
| `login(user_id, password)` | 是 | `00` LOGIN_REQUEST | `01` LOGIN_RESPONSE | `LoginAsync` |
| `logout(user_id)` | 是 | `02` LOGOUT_REQUEST | `03` LOGOUT_RESPONSE | `LogoutAsync` |
| `set_API_key(apiKey)` | 是 | （无网络请求，仅设置会话状态） | — | `SetApiKey` |

> 错误响应统一使用 `04` MESSAGE_TYPE_EXCEPTIONS。

### 2.2 历史 K 线（`reference/baostock-python/security/history.py`）

| Python 函数 | 导出 | MSG 请求 | MSG 响应 | C# 目标方法 |
| --- | --- | --- | --- | --- |
| `query_history_k_data_plus(code, fields, start_date, end_date, frequency, adjustflag)` | 是 | `95` GETKDATAPLUS_REQUEST | `96` GETKDATAPLUS_RESPONSE | `QueryHistoryKDataPlusAsync` |

> ⚠ 校正：`11/12` GETKDATA_REQUEST/RESPONSE 是上游残留的旧编号常量，源码 `query_history_k_data_plus` **实际使用的是 `95/96`**（且 `96` 在 `COMPRESSED_MESSAGE_TYPE_TUPLE` 中需 zlib 解压）。

### 2.3 板块成分与分类（`reference/baostock-python/security/sectorinfo.py`）

| Python 函数 | 导出 | MSG 请求 | MSG 响应 |
| --- | --- | --- | --- |
| `query_stock_industry(code, date)` | 是 | `59` | `60` |
| `query_hs300_stocks(date)` | 是 | `61` | `62` |
| `query_sz50_stocks(date)` | 是 | `63` | `64` |
| `query_zz500_stocks(date)` | 是 | `65` | `66` |
| `query_terminated_stocks(date)` | **是（v1.0 必交付，实测可用）** | `67` | `68` |
| `query_suspended_stocks(date)` | **是（v1.0 必交付，实测可用）** | `69` | `70` |
| `query_st_stocks(date)` | **是（v1.0 必交付，实测可用）** | `71` | `72` |
| `query_starst_stocks(date)` | **是（v1.0 必交付，实测可用）** | `73` | `74` |
| `query_stock_concept(code, date)` | 否（**服务端已下线 ec=10004020，不实现**） | `81` | `82` |
| `query_stock_area(code, date)` | 否（**服务端已下线 ec=10004020，不实现**） | `83` | `84` |
| `query_ame_stocks(date)` | 否（**服务端已下线 ec=10004020，不实现**） | `85` | `86` |
| `query_gem_stocks(date)` | 否（**服务端已下线 ec=10004020，不实现**） | `87` | `88` |
| `query_shhk_stocks(date)` | 否（**服务端已下线 ec=10004020，不实现**） | `89` | `90` |
| `query_szhk_stocks(date)` | 否（**服务端已下线 ec=10004020，不实现**） | `91` | `92` |
| `query_stocks_in_risk(date)` | 否（**服务端已下线 ec=10004020，不实现**） | `93` | `94` |

### 2.4 季频估值与财务（`reference/baostock-python/evaluation/season_index.py`）

| Python 函数 | 导出 | MSG 请求 | MSG 响应 |
| --- | --- | --- | --- |
| `query_dividend_data(code, year, yearType)` | 是 | `13` | `14` |
| `query_adjust_factor(code, start_date, end_date)` | 是 | `15` | `16` |
| `query_profit_data(code, year, quarter)` | 是 | `17` | `18` |
| `query_operation_data(code, year, quarter)` | 是 | `19` | `20` |
| `query_growth_data(code, year, quarter)` | 是 | `21` | `22` |
| `query_dupont_data(code, year, quarter)` | 是 | `23` | `24` |
| `query_balance_data(code, year, quarter)` | 是 | `25` | `26` |
| `query_cash_flow_data(code, year, quarter)` | 是 | `27` | `28` |

### 2.5 公司公告（`reference/baostock-python/corpreport/corp_performance.py`）

| Python 函数 | 导出 | MSG 请求 | MSG 响应 |
| --- | --- | --- | --- |
| `query_performance_express_report(code, start_date, end_date)` | 是 | `29` | `30` |
| `query_forecast_report(code, start_date, end_date)` | 是 | `31` | `32` |

### 2.6 元数据（`reference/baostock-python/metadata/stock_metadata.py`）

| Python 函数 | 导出 | MSG 请求 | MSG 响应 |
| --- | --- | --- | --- |
| `query_trade_dates(start_date, end_date)` | 是 | `33` | `34` |
| `query_all_stock(day)` | 是 | `35` | `36` |
| `query_stock_basic(code, code_name)` | 是 | `45` | `46` |

### 2.7 宏观经济（`reference/baostock-python/macroscopic/economic_data.py`）

| Python 函数 | 导出 | MSG 请求 | MSG 响应 |
| --- | --- | --- | --- |
| `query_deposit_rate_data(start_date, end_date)` | 是 | `47` | `48` |
| `query_loan_rate_data(start_date, end_date)` | 是 | `49` | `50` |
| `query_required_reserve_ratio_data(start_date, end_date, yearType)` | 是 | `51` | `52` |
| `query_money_supply_data_month(start_date, end_date)` | 是 | `53` | `54` |
| `query_money_supply_data_year(start_date, end_date)` | 是 | `55` | `56` |
| `query_cpi_data(start_date, end_date)` | 否（**服务端已下线 ec=10004020，不实现**） | `75` | `76` |
| `query_ppi_data(start_date, end_date)` | 否（**服务端已下线 ec=10004020，不实现**） | `77` | `78` |
| `query_pmi_data(start_date, end_date)` | 否（**服务端已下线 ec=10004020，不实现**） | `79` | `80` |

### 2.8 仅有协议常量、无 Python 实现（保留位）

| 常量 | MSG 请求 | MSG 响应 | 备注 |
| --- | --- | --- | --- |
| `MESSAGE_TYPE_QUERYSHIBORDATA_*` | `57` | `58` | Python `00.9.10` 中仅有常量，无对应 `query_shibor_*` 函数。Baostock.NET v1.0 **不实现**，仅在 `Constants.cs` 保留枚举值，待上游公开后再补。 |

### 2.9 v1.0 必交付的"导出 API"总数

v1.0 必交付共 **27 个**公开方法：`login`、`logout`、`set_API_key`（3 会话态）+ 24 个 `query_*`：

- K 线 1：`query_history_k_data_plus`
- 板块 8：`query_stock_industry`、`query_hs300_stocks`、`query_sz50_stocks`、`query_zz500_stocks`、`query_terminated_stocks`、`query_suspended_stocks`、`query_st_stocks`、`query_starst_stocks`（其中后 4 个虽未在 `__init__.py` 导出，但服务端实测可用，依据 [tests/Fixtures/_candidates/](../tests/Fixtures/_candidates/) 的抓包结果纳入 v1.0）
- 季频财务 8、公司公告 2、元数据 3、宏观 5

候选清单中其余 10 个（`query_cpi_data`/`ppi`/`pmi`/`stock_concept`/`stock_area`/`ame_stocks`/`gem_stocks`/`shhk_stocks`/`szhk_stocks`/`stocks_in_risk`）经实测**服务端返回 `MSG=04` 错误码 `10004020`（错误的消息类型）**，已下线，详见 [tests/Fixtures/_skipped.md](../tests/Fixtures/_skipped.md)，v1.0 不实现。

这 27 个就是 §6 v1.0.0 GA 的硬验收范围。

---

## 3. 协议复刻规范

> 全部依据 `reference/baostock-python/common/contants.py`、`data/messageheader.py`、`data/msg.py`、`data/resultset.py`、`util/socketutil.py` 已实测的协议事实。

### 3.1 网络层

- 服务器：`public-api.baostock.com`（`BAOSTOCK_SERVER_IP`）
- 端口：`10030`（`BAOSTOCK_SERVER_PORT`）
- 协议：单连接长会话 TCP，文本 + 长度前缀 + 校验
- 编码：UTF-8（请求与响应 body 均按 UTF-8 编解码）

### 3.2 帧格式（一条完整消息）

```
[ MESSAGE_HEADER (21 字节, ASCII) ] [ BODY (UTF-8, body_length 字节) ] \x01 [ CRC32 (变长十进制 ASCII, 不补零) ] \n
```

> CRC32 = `zlib.crc32(header || body)` 的十进制 ASCII 字符串，**变长、不补零**，紧跟在 `\x01` 之后、`\n` 之前。已通过 [tests/Fixtures/login/request.bin](../tests/Fixtures/login/request.bin) 实测验证（值=359766228，长度 9 字节）。.NET 端用 `System.IO.Hashing.Crc32`，与 Python `zlib.crc32` 输出完全一致。

- `MESSAGE_HEADER_LENGTH = 21`
- `MESSAGE_SPLIT = "\x01"`：消息内部各段分隔符
- `DELIMITER = "\n"`：一条消息的物理结束符
- `ATTRIBUTE_SPLIT = ","`：body 内同一字段中的子参数分隔（如 `fields` 列表）

### 3.3 消息头结构（21 字节定长 ASCII）

由 `data/messageheader.to_message_header` 生成，字段以 `\x01` 串接：

```
<BAOSTOCK_CLIENT_VERSION="00.9.10"> \x01 <msg_type 2 位> \x01 <total_msg_length 10 位, 左补零>
```

校验：`5 + 1 + 2 + 1 + 10 = 19` ASCII 字符 + 末尾若上游再追加 2 字节填充凑齐 21 → 实现时以 `MESSAGE_HEADER_LENGTH` 为准、按位读取，**不要写死字段偏移**，交由 `MessageHeader` 解析器拆分。

> `total_msg_length` 是包含 body 在内的总长（参考 Python `add_zero_for_string(..., 10, True)`），实现时读端以这个数字为权威。

### 3.4 body 与压缩

- body 内字段间用 `\x01` 分隔；某字段是列表时（如 K 线 `fields`）用 `,` 内分隔
- `COMPRESSED_MESSAGE_TYPE_TUPLE = (MESSAGE_TYPE_GETKDATAPLUS_RESPONSE,)` —— 仅 `96` 响应使用 zlib 压缩 body；其余 msg_type 不压缩
- 请求方向 v0.x 暂无压缩需求

### 3.5 接收策略

`util/socketutil.py` 中按"小消息一帧到底 / 大消息分次读"两条路径接收。.NET 实现统一为：

1. 先读 21 字节头 → 解析 `total_msg_length`
2. 按 `total_msg_length - 21` 计算 body+校验+`\n` 长度，循环 `ReadAsync` 直到读满
3. 取末尾 `\n` 之前的 10 位 ASCII 作为 CRC32 字符串，body 段为 `[21, total_len - 1 - 10 - 1)`
4. 校验 CRC32（与 body 字符串的 `Crc32` 对比，使用 `System.IO.Hashing.Crc32`）
5. 若 msg_type 在压缩白名单内，对 body 做 `zlib`（`System.IO.Compression.ZLibStream`，`.NET 6+` 内置）解压

### 3.6 错误码

`reference/baostock-python/common/contants.py` 中 `BSERR_*` 常量 1:1 翻译为 `BaostockErrorCode` enum（成员名去掉 `BSERR_` 前缀，值保留为字符串常量），并附 `[Description]` 中文说明。所有非 `BSERR_SUCCESS` 的服务端响应统一抛 `BaostockException(code, message)`。

**错误帧约定**（依据 [tests/Fixtures/_candidates/](../tests/Fixtures/_candidates/) 的实测）：服务端遇到未知或已下线的 `MSG` 类型**不会断连**，而是返回一个 `MSG=04`（`MESSAGE_TYPE_EXCEPTIONS`）的错误响应帧，body 形如 `<error_code>\x01<error_msg>`（例如 `10004020\x01错误的消息类型`）。.NET `BaostockTransport` 在收到 `MSG=04` 帧时**必须**解析 body 的前两个 `\x01` 分段为 `(error_code, error_msg)` 并抛出 `BaostockException(error_code, error_msg)`，**不得**把它当作正常响应交给上层 query 解码器。

### 3.7 服务端 BodyLength 字段不可信（实测）

服务端响应头里的 `BodyLength` 字段，对**含中文字符的 body** 填的是**字符数**而非 UTF-8 字节数（典型差异：54 vs 70）。但 CRC32 仍然按真实字节计算。因此 .NET 实现**不能**用 `BodyLength` 切 body：
- 非压缩响应：靠 `<![CDATA[]]>\n` 物理结尾标记收流
- body 与 CRC 的边界：靠**最后一个 `\x01`**（CRC 是纯 ASCII 数字，永不含 `\x01`）
- 已通过 v0.2.0 补测验证（commit `6d993bb`）。

### 3.8 .NET 协议层契约（强约束）

- `BaostockProtocol`：纯静态/纯函数层，负责帧编解码、CRC32、zlib，**不引用 socket、不引用 client、可单测**
- `BaostockTransport`：抽象 `ITransport { Task ConnectAsync; Task SendAsync(ReadOnlyMemory<byte>); ValueTask<ReadOnlyMemory<byte>> ReceiveOneFrameAsync(); }`，默认实现 `TcpTransport`，测试可注入 `FixtureTransport`
- `BaostockClient`：会话状态机，持有 `user_id`、`apiKey`、登录态、序列号；所有公开 API 入口
- 全部对外 API 一律 `async/await`，签名返回 `Task<T>` 或 `IAsyncEnumerable<TRow>`；不暴露同步阻塞方法

---

## 4. .NET API 映射约定

### 4.1 命名

| Python | C# |
| --- | --- |
| `bs.login('user', 'pwd')` | `await client.LoginAsync("user", "pwd")` |
| `bs.query_history_k_data_plus(...)` | `client.QueryHistoryKDataPlusAsync(...)` |
| `bs.set_API_key('xxx')` | `client.SetApiKey("xxx")` |
| 入参 `start_date` | `startDate`（camelCase） |
| 入参 `yearType` | `yearType`（保留） |

### 4.2 返回值

Python 端返回 `ResultSetData`（含 `fields`、`data` 二维表，调用 `next()` + `get_row_data()` 行游标）。.NET 端**对每个 query** 同时提供：

- 流式：`IAsyncEnumerable<TRow> QueryXxxAsync(...)` —— 内部按帧分页拉取并 yield
- 一次拉完：`IAsyncEnumerable<TRow>.ToListAsync()`（基于扩展方法）

### 4.3 强类型 Row

每个 query 一个 `record`，字段命名 PascalCase，字段顺序与 Python 返回 `fields` 一致：

- `KLineRow`、`DividendRow`、`AdjustFactorRow`、`ProfitRow`、`OperationRow`、`GrowthRow`、`DupontRow`、`BalanceRow`、`CashFlowRow`、`PerformanceExpressRow`、`ForecastRow`、`TradeDateRow`、`AllStockRow`、`StockBasicRow`、`StockIndustryRow`、`Hs300StockRow`、`Sz50StockRow`、`Zz500StockRow`、`DepositRateRow`、`LoanRateRow`、`RequiredReserveRatioRow`、`MoneySupplyMonthRow`、`MoneySupplyYearRow`

### 4.4 字段类型推断

| 字段语义 | Python 字符串 | C# 类型 |
| --- | --- | --- |
| 日期（`date`、`pubDate`、`statDate`、`tradeDate`） | `"YYYY-MM-DD"` | `DateOnly` |
| 时间（K 线 `time`，`YYYYMMDDHHmmssfff`） | string | `DateTime`（UTC 标记） |
| 价格、金额（`open/high/low/close/preclose/turn/pctChg/peTTM/...`） | string | `decimal?` |
| 成交量、成交额、成交笔数 | string | `long?` / `decimal?` |
| 比率（`liqaShare/...`） | string | `decimal?` |
| 整型字段（`tradestatus`、`adjustflag`） | string | `int?` 或 enum |
| 布尔（`isST`） | `"0"/"1"` | `bool` |

> 任何字段值为空字符串一律映射为 `null`，**不做 0 兜底**。

### 4.5 枚举

- `KLineFrequency`：`Daily="d"`, `Weekly="w"`, `Monthly="m"`, `Min5="5"`, `Min15="15"`, `Min30="30"`, `Min60="60"`
- `AdjustFlag`：`Forward="1"`, `Backward="2"`, `None="3"`（默认 `"3"` 不复权）
- `YearType`（红利）：`Report="report"`, `Operate="operate"`
- `RequiredReserveYearType`：`All="0"`, `Big="1"`, `Medium="2"`

对外暴露 enum，对内由 `EnumStringConverter` 统一序列化回原字符串（与 Python wire 格式一致）。

---

## 5. 项目结构（v1.0 最终态）

```
src/Baostock.NET/
  Protocol/
    Constants.cs              // 端口、MSG 类型码、错误码、版本号
    MessageHeader.cs          // 21 字节头编/解
    FrameCodec.cs             // 帧编解码 + CRC32 + zlib
    BaostockTransport.cs      // ITransport + TcpTransport
    BaostockErrorCode.cs
  Client/
    BaostockClient.cs         // 主入口（partial）
    BaostockSession.cs        // user_id / apiKey / 登录态
    BaostockException.cs
    BaostockClientOptions.cs  // host / port / timeout / logger
  Models/
    KLineRow.cs
    DividendRow.cs
    AdjustFactorRow.cs
    ProfitRow.cs
    OperationRow.cs
    GrowthRow.cs
    DupontRow.cs
    BalanceRow.cs
    CashFlowRow.cs
    PerformanceExpressRow.cs
    ForecastRow.cs
    TradeDateRow.cs
    AllStockRow.cs
    StockBasicRow.cs
    StockIndustryRow.cs
    Hs300StockRow.cs
    Sz50StockRow.cs
    Zz500StockRow.cs
    DepositRateRow.cs
    LoanRateRow.cs
    RequiredReserveRatioRow.cs
    MoneySupplyMonthRow.cs
    MoneySupplyYearRow.cs
  Queries/                    // 每组一个 partial class on BaostockClient
    Client.Auth.cs
    Client.History.cs
    Client.Sector.cs
    Client.Evaluation.cs
    Client.Corp.cs
    Client.Metadata.cs
    Client.Macro.cs
  Internal/
    AsyncEnumerableExtensions.cs   // ToListAsync 等
    EnumStringConverter.cs

tests/Baostock.NET.Tests/
  Protocol/                   // 纯协议层单测，不联网
    FrameCodecTests.cs
    MessageHeaderTests.cs
    Crc32Tests.cs
    ZLibTests.cs
  Client/                     // 用 FixtureTransport 注入回放
    LoginTests.cs
    QueryHistoryKDataPlusTests.cs
    ...
  Integration/                // 走真实服务器；[Trait("Category","Live")]
    LiveLoginTests.cs
    LiveQueryHistoryKDataPlusTests.cs
  Fixtures/                   // 抓包后的二进制样本
    login.req.bin
    login.resp.bin
    history_kline.req.bin
    history_kline.resp.bin
    ...

tools/
  compare-with-python/        // §7 黄金对比脚本
    run_compare.ps1
    run_compare.py
  capture-fixtures/           // §7 抓包脚本
    capture.py
docs/
  PROJECT-PLAN.md             // 本文
  api/                        // DocFX 生成
  index.md
  toc.yml
  docfx.json
```

---

## 6. 里程碑与版本规划

| 版本 | 范围 | 验收硬指标 |
| --- | --- | --- |
| **v0.1.0 协议层** | `Protocol/Constants.cs` 全常量、`MessageHeader`、`FrameCodec`（含 CRC32、zlib） | 单测覆盖率 ≥ 95%；用 §7 fixture 字节流编/解码往返一致 |
| **v0.2.0 登录会话** | `LoginAsync` / `LogoutAsync` / `SetApiKey`、`BaostockSession`、`BaostockException` | `Live` 集成测试可成功 `LoginAsync("anonymous","123456")` 并 `LogoutAsync` |
| **v0.3.0 K 线** | `QueryHistoryKDataPlusAsync`、`KLineRow`、压缩响应解码 | 与 Python 客户端对同一 `(code, fields, dates, frequency, adjustflag)` 输出 CSV 逐字段 diff = 0 |
| **v0.4.0 板块+元数据+季频财务** | 4 板块 + 3 元数据 + 8 季频，共 **15** 个 query | 每个 query 都有 fixture 单测 + Live 测试 + Python diff = 0 |
| **v0.5.0 宏观+公司公告** | 5 宏观 + 2 公告，共 **7** 个 query | 同上 |
| **v0.9.0 RC** | 全 23 个导出 API + DocFX 文档站初稿 + NuGet pre-release（`-rc.1`） | CI 全绿；文档站可访问；`dotnet add package Baostock.NET --prerelease` 可装 |
| **v1.0.0 GA** | 文档完整、CI 绿、NuGet 正式包、README 中英对照表 | tag `v1.0.0` 触发 CI 自动 `dotnet pack` + push to nuget.org |

每个里程碑结束都必须产出：(a) 对应单元测试 PR；(b) 与 Python 客户端的 diff 报告 CSV，**任何字段不一致即视为 release blocker**。

---

## 7. 测试策略

### 7.1 分层

- **Unit / Protocol**：覆盖 `FrameCodec`、`MessageHeader`、`BaostockErrorCode`、CRC32、zlib，全部纯函数，目标覆盖率 **≥ 95%**
- **Unit / Client (Fixture)**：用 `FixtureTransport` 把抓包字节流注入 `BaostockClient`，验证每个 `QueryXxxAsync` 的解析、流式分页、异常路径。Client 层覆盖率 **≥ 80%**
- **Integration / Live**：标注 `[Trait("Category","Live")]`；走真实 `public-api.baostock.com:10030`。CI 默认跳过（`dotnet test --filter "Category!=Live"`），本地与 nightly 必跑

### 7.2 录制回放（fixtures）

- 一次性脚本 `tools/capture-fixtures/capture.py`：`monkeypatch` Python 客户端的 socket `send/recv`，把每个 query 的 `(req_bytes, resp_bytes)` 落盘到 `tests/Baostock.NET.Tests/Fixtures/<query_name>.{req,resp}.bin`
- C# 单测从这两份 bin 反序列化，断言 (i) 客户端编出来的请求字节流与 `req.bin` 一致；(ii) 用 `resp.bin` 喂回来后，得到的 row 列表与"Python 跑出来的 CSV"一致

### 7.3 黄金对比测试（与 Python diff）

- `tools/compare-with-python/`：同一组参数，`run_compare.py` 跑 Python 客户端导出 CSV、`run_compare.ps1` 跑 .NET CLI 演示程序导出 CSV，再用脚本逐字段 diff
- 接入 GitHub Actions 的 `nightly` workflow，diff 非空即 fail（artifact 上传 diff 报告）

### 7.4 限流与稳定性

- Live 测试做指数退避：失败重试 3 次，初始 500ms，最大 8s
- Live 测试串行（`xunit.runner.json` 中关闭并行），避免触发服务端连接频率限制

---

## 8. 文档与发布

### 8.1 README 与文档站

- `README.md`（仓库根，本里程碑不修改）：v0.9.0 RC 时重写，包含：5 行快速上手、Python ↔ .NET API 对照表（链接到 §2 的 23 个方法）、License 声明
- `docs/`：使用 **DocFX** 生成静态站点；通过 GitHub Actions 在 `main` 分支构建并部署到 `gh-pages`
- 每个公开方法：**XML doc 注释（中英双语）** + 与 Python 调用对照的代码片段；DocFX 自动按 query 生成单独页面，结构对齐 baostock.com 官方文档章节

### 8.2 NuGet 包

`src/Baostock.NET/Baostock.NET.csproj` 必须填齐：

- `<PackageId>Baostock.NET</PackageId>`
- `<PackageReadmeFile>README.md</PackageReadmeFile>`
- `<PackageLicenseExpression>MIT</PackageLicenseExpression>`
- `<RepositoryUrl>` / `<RepositoryType>git</RepositoryType>`
- `<PackageProjectUrl>` 指向文档站
- `<PackageTags>baostock;a-share;china-stock;finance;market-data</PackageTags>`
- `<IncludeSymbols>true</IncludeSymbols>` + `snupkg`
- `<Deterministic>true</Deterministic>` + `<ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>`（仅 CI 上为 true）

### 8.3 GitHub Actions

- **`pr.yml`**（PR）：`dotnet restore` → `dotnet build -c Release` → `dotnet test --filter "Category!=Live"`
- **`main.yml`**（push to main）：上同 + 可选 Live + DocFX build → 部署到 `gh-pages`
- **`release.yml`**（tag `v*`）：`dotnet pack -c Release` → `dotnet nuget push` 到 `nuget.org`（推荐 OIDC trusted publishing，回退方案 `NUGET_API_KEY` secret）

---

## 9. 风险与约束

| 风险 | 缓解 |
| --- | --- |
| 上游协议无官方规范文档，全靠 Python 源码反推 | §7.2 抓包 fixture 沉淀为回归基线；任何疑点都以"实抓字节流"为准 |
| 服务端有匿名限流/限频 | Live 测试串行 + 指数退避；nightly 跑而非 PR 跑 |
| Python 端 `print()` 与全局单例（`bs._g_user_id` 等）副作用 | .NET 端不复制；改为显式 `BaostockClient` 实例 + `BaostockException` 抛错；不提供模块级静态 API |
| K 线响应分页与压缩组合的边界（`COMPRESSED_MESSAGE_TYPE_TUPLE` 仅含 `96`） | `FrameCodec` 通过 msg_type 查表决定是否解压，单测覆盖压缩与非压缩两条路径 |
| License 兼容 | 上游 `baostock` 包的 license 在发布前需在 README 中明确：本项目仅参考其公开协议、`reference/` 目录仅用于研究、产物中不包含其源码 |
| Shibor (`57/58`) 等仅有常量、无实现的 MSG | v1.0 不实现，仅在 `Constants.cs` 留枚举，避免误用 |
| 错误处理风格（异常 vs Result） | **v0.1.0 定稿**：所有"用户操作失败 / 服务端非 0 错误码"统一抛 `BaostockException(BaostockErrorCode, string message)`；不引入 `Result<T>` |

---

## 10. 立刻可做的下一步（按 PR 拆分）

1. **PR-1 `protocol/constants`**：新建 `src/Baostock.NET/Protocol/Constants.cs` + `BaostockErrorCode.cs`，把 `reference/baostock-python/common/contants.py` 全部 `MESSAGE_TYPE_*`、`BSERR_*`、`BAOSTOCK_*`、`MESSAGE_*`、`DELIMITER`、`ATTRIBUTE_SPLIT`、`COMPRESSED_MESSAGE_TYPE_TUPLE` 翻译到位（含中文注释）。零业务逻辑、零依赖。
2. **PR-2 `protocol/codec`**：实现 `MessageHeader`（编/解 21 字节头）+ `FrameCodec`（封帧/拆帧 + CRC32 + zlib），配套 `tests/Baostock.NET.Tests/Protocol/*Tests.cs`。CRC32 用 `System.IO.Hashing`，zlib 用 `System.IO.Compression.ZLibStream`。
3. **PR-3 `transport+login`**：`ITransport` + `TcpTransport`（基于 `System.Net.Sockets.TcpClient`，全 async）+ `BaostockClient` 骨架 + `Client.Auth.cs` 的 `LoginAsync` / `LogoutAsync` / `SetApiKey`，加一条 `tests/Integration/LiveLoginTests.cs` 真连一次 `public-api.baostock.com:10030`。

> 上述三个 PR 完成即达成 v0.2.0。后续 query 按 §6 节奏推进，每个 query **必带 fixture 单测 + Live 测试 + Python diff 报告**，否则不合并。
