# *ST股票

## 方法说明

查询被特别处理且面临退市风险（*ST）的股票列表。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_starst_stocks()` | `client.QueryStarStStocksAsync()` |

## 方法签名

```csharp
public async IAsyncEnumerable<SpecialStockRow> QueryStarStStocksAsync(
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

await foreach (var row in client.QueryStarStStocksAsync())
{
    Console.WriteLine($"{row.Code} {row.CodeName}");
}
```

## 数据范围

- 包含当前被 *ST（退市风险警示）的 A 股股票
- *ST 标记表示该公司存在退市风险
