# 交易日查询

## 方法说明

查询交易日历，判断指定日期区间内每天是否为交易日。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_trade_dates()` | `client.QueryTradeDatesAsync()` |

## 方法签名

```csharp
public async IAsyncEnumerable<TradeDateRow> QueryTradeDatesAsync(
    string? startDate = null,
    string? endDate = null,
    CancellationToken ct = default)
```

## 参数说明

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| startDate | string? | 否 | 开始日期，格式 `"yyyy-MM-dd"`，默认 `"2015-01-01"` |
| endDate | string? | 否 | 结束日期，格式 `"yyyy-MM-dd"`，默认当天 |
| ct | CancellationToken | 否 | 取消令牌 |

## 返回字段

| 字段 | 类型 | 说明 |
|------|------|------|
| Date | DateOnly | 日期 |
| IsTrading | bool | 是否为交易日（`true` 为交易日） |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryTradeDatesAsync(startDate: "2024-01-01", endDate: "2024-01-31"))
{
    if (row.IsTrading)
        Console.WriteLine($"{row.Date} 交易日");
}
```

## 数据范围

- 数据从 1990-12-19 开始
- 包含未来已公布的交易日安排
