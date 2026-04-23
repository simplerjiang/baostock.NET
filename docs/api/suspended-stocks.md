# 暂停上市股票

## 方法说明

查询暂停上市的股票列表。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_suspended_stocks()` | `client.QuerySuspendedStocksAsync()` |

## 方法签名

```csharp
public async IAsyncEnumerable<SpecialStockRow> QuerySuspendedStocksAsync(
    string? date = null,
    CancellationToken ct = default)
```

## 参数说明

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| date | string? | 否 | 查询日期，格式 `"yyyy-MM-dd"`。为空时返回最新 |
| ct | CancellationToken | 否 | 取消令牌 |

## 返回字段

| 字段 | 类型 | 说明 |
|------|------|------|
| UpdateDate | string | 更新日期 |
| Code | string | 证券代码 |
| CodeName | string | 证券名称 |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QuerySuspendedStocksAsync())
{
    Console.WriteLine($"{row.Code} {row.CodeName} 更新:{row.UpdateDate}");
}
```

## 数据范围

- 包含当前处于暂停上市状态的 A 股股票
