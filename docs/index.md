# Baostock.NET 文档

Baostock.NET 是 [baostock](http://baostock.com) 官方 Python 客户端的纯 .NET 9 复刻实现，提供中国 A 股市场数据查询服务。

## 与 Python baostock 的关系

- **API 一一对应**：每个 Python `bs.query_xxx()` 方法都有对应的 `client.QueryXxxAsync()` 方法
- **协议兼容**：连接同一服务端 `public-api.baostock.com:10030`，返回数据逐字段对齐
- **强类型**：所有返回值均为 C# record，类型安全、IDE 友好
- **流式消费**：基于 `IAsyncEnumerable<T>`，自动分页拉取，内存友好

## 安装

```shell
dotnet add package Baostock.NET
```

详见 [快速开始](getting-started.md)。

## API 导航

### 登录 / 会话

| API | 说明 |
|-----|------|
| [登录登出](api/login.md) | `LoginAsync` / `LogoutAsync` / `SetApiKey` |

### K 线数据

| API | 说明 |
|-----|------|
| [A股K线数据](api/history-k-data.md) | 日/周/月/分钟级 K 线 |

### 估值财务（季频）

| API | 说明 |
|-----|------|
| [除权除息信息](api/dividend-data.md) | 分红送股明细 |
| [复权因子信息](api/adjust-factor.md) | 前复权/后复权因子 |
| [季频盈利能力](api/profit-data.md) | ROE、净利率、毛利率 |
| [季频营运能力](api/operation-data.md) | 应收账款周转率等 |
| [季频成长能力](api/growth-data.md) | 净资产/净利润同比增长率 |
| [季频偿债能力](api/balance-data.md) | 流动比率、速动比率 |
| [季频现金流量](api/cash-flow-data.md) | 现金流量指标 |
| [季频杜邦指数](api/dupont-data.md) | 杜邦分析指标 |

### 公告报告

| API | 说明 |
|-----|------|
| [季频业绩快报](api/performance-express-report.md) | 业绩快报数据 |
| [季频业绩预告](api/forecast-report.md) | 业绩预告数据 |

### 元数据

| API | 说明 |
|-----|------|
| [证券基本资料](api/stock-basic.md) | 证券代码、名称、上市日期 |
| [交易日查询](api/trade-dates.md) | 交易日历 |
| [证券代码查询](api/all-stock.md) | 指定日全部证券列表 |

### 板块指数

| API | 说明 |
|-----|------|
| [行业分类](api/stock-industry.md) | 行业分类信息 |
| [上证50成分股](api/sz50-stocks.md) | 上证50指数成分股 |
| [沪深300成分股](api/hs300-stocks.md) | 沪深300指数成分股 |
| [中证500成分股](api/zz500-stocks.md) | 中证500指数成分股 |

### 宏观经济

| API | 说明 |
|-----|------|
| [存款利率](api/deposit-rate.md) | 存款基准利率 |
| [贷款利率](api/loan-rate.md) | 贷款基准利率 |
| [存款准备金率](api/reserve-ratio.md) | 法定存款准备金率 |
| [货币供应量(月度)](api/money-supply-month.md) | M0/M1/M2 月度数据 |
| [货币供应量(年底余额)](api/money-supply-year.md) | M0/M1/M2 年度余额 |

### 特殊股票

| API | 说明 |
|-----|------|
| [退市股票](api/terminated-stocks.md) | 终止上市股票列表 |
| [暂停上市股票](api/suspended-stocks.md) | 暂停上市股票列表 |
| [ST股票](api/st-stocks.md) | ST 股票列表 |
| [*ST股票](api/starst-stocks.md) | *ST 股票列表 |
