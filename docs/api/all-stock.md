# 证券代码查询

## 方法说明

查询指定日期的全部证券列表，包括股票、指数等。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_all_stock()` | `client.QueryAllStockAsync()` |

## 方法签名

```csharp
public async IAsyncEnumerable<StockListRow> QueryAllStockAsync(
    string? day = null,
    CancellationToken ct = default)
```

## 参数说明

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| day | string? | 否 | 查询日期，格式 `"yyyy-MM-dd"`，默认当天 |
| ct | CancellationToken | 否 | 取消令牌 |

## 返回字段

| 字段 | 类型 | 说明 |
|------|------|------|
| Code | string | 证券代码 |
| TradeStatus | string | 交易状态（1: 正常交易，0: 停牌） |
| CodeName | string | 证券名称 |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryAllStockAsync(day: "2024-01-02"))
{
    Console.WriteLine($"{row.Code} {row.CodeName} 状态:{row.TradeStatus}");
}
```

## 数据范围

- 返回指定交易日的全部可交易证券
- 包括股票、指数等
