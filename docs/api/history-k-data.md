# A股K线数据

## 方法说明

查询 A 股历史 K 线数据，支持日/周/月频率，支持前复权/后复权/不复权。数据自动按需分页拉取，流式返回每行。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_history_k_data_plus()` | `client.QueryHistoryKDataPlusAsync()` |

## 方法签名

```csharp
public async IAsyncEnumerable<KLineRow> QueryHistoryKDataPlusAsync(
    string code,
    string? fields = null,
    string? startDate = null,
    string? endDate = null,
    KLineFrequency frequency = KLineFrequency.Day,
    AdjustFlag adjustFlag = AdjustFlag.PreAdjust,
    CancellationToken ct = default)
```

## 参数说明

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| code | string | 是 | 证券代码，如 `"sh.600000"`、`"sz.000001"` |
| fields | string? | 否 | 返回字段列表，逗号分隔，默认全部日频字段 |
| startDate | string? | 否 | 开始日期，格式 `"yyyy-MM-dd"`，默认 `"2017-07-01"` |
| endDate | string? | 否 | 结束日期，格式 `"yyyy-MM-dd"`，默认当天 |
| frequency | KLineFrequency | 否 | K线频率，默认 `Day` |
| adjustFlag | AdjustFlag | 否 | 复权类型，默认 `PreAdjust`（前复权） |
| ct | CancellationToken | 否 | 取消令牌 |

### KLineFrequency 枚举

| 值 | 说明 |
|----|------|
| Day | 日线 |
| Week | 周线 |
| Month | 月线 |
| FiveMinute | 5 分钟线 |
| FifteenMinute | 15 分钟线 |
| ThirtyMinute | 30 分钟线 |
| SixtyMinute | 60 分钟线 |

### AdjustFlag 枚举

| 值 | 说明 |
|----|------|
| PostAdjust (1) | 后复权 |
| PreAdjust (2) | 前复权 |
| NoAdjust (3) | 不复权 |

## 返回字段

| 字段 | 类型 | 说明 |
|------|------|------|
| Date | DateOnly | 交易日期 |
| Code | string | 证券代码 |
| Open | decimal? | 开盘价 |
| High | decimal? | 最高价 |
| Low | decimal? | 最低价 |
| Close | decimal? | 收盘价 |
| PreClose | decimal? | 前收盘价 |
| Volume | long? | 成交量（股） |
| Amount | decimal? | 成交额（元） |
| AdjustFlag | AdjustFlag | 复权状态 |
| Turn | decimal? | 换手率（%） |
| TradeStatus | TradeStatus | 交易状态 |
| PctChg | decimal? | 涨跌幅（%） |
| IsST | bool | 是否 ST 股 |

### TradeStatus 枚举

| 值 | 说明 |
|----|------|
| Suspended (0) | 停牌 |
| Normal (1) | 正常交易 |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryHistoryKDataPlusAsync(
    "sh.600000",
    startDate: "2024-01-01",
    endDate: "2024-01-31"))
{
    Console.WriteLine($"{row.Date} 开:{row.Open} 高:{row.High} 低:{row.Low} 收:{row.Close} 量:{row.Volume}");
}
```

## 数据范围

- 日线数据从 1990-12-19 开始
- 周线、月线数据从 1990-12-19 开始
- 5/15/30/60 分钟线数据从 1999-07-26 开始
- 数据每日 18:00 左右更新

## 分钟频率

### 方法签名

```csharp
public async IAsyncEnumerable<MinuteKLineRow> QueryHistoryKDataPlusMinuteAsync(
    string code,
    string? fields = null,
    string? startDate = null,
    string? endDate = null,
    KLineFrequency frequency = KLineFrequency.FiveMinute,
    AdjustFlag adjustFlag = AdjustFlag.PreAdjust,
    CancellationToken ct = default)
```

### MinuteKLineRow 返回字段

| 字段 | 类型 | 说明 |
|------|------|------|
| Date | DateOnly | 交易日期 |
| Time | string | Bar 结束时间，17 位 `YYYYMMDDHHmmssSSS` 格式，如 `"20240102093500000"` |
| Code | string | 证券代码 |
| Open | decimal? | 开盘价 |
| High | decimal? | 最高价 |
| Low | decimal? | 最低价 |
| Close | decimal? | 收盘价 |
| Volume | long? | 成交量（股） |
| Amount | decimal? | 成交额（元） |
| AdjustFlag | AdjustFlag | 复权状态 |

> **`Time` 字段说明**：17 位字符串，格式为 `YYYYMMDDHHmmssSSS`（年月日时分秒毫秒），表示该 bar 的结束时间。使用 `string` 而非 `TimeOnly`，因为该格式含日期与毫秒，用户可按需自行转换。

### 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryHistoryKDataPlusMinuteAsync(
    "sh.600000",
    startDate: "2024-01-02",
    endDate: "2024-01-03",
    frequency: KLineFrequency.FiveMinute))
{
    Console.WriteLine($"{row.Date} {row.Time} 开:{row.Open} 高:{row.High} 低:{row.Low} 收:{row.Close} 量:{row.Volume}");
}
```

### 数据范围

近 5 年（2020-01-03 至今），数据每日 18:00 左右更新。
