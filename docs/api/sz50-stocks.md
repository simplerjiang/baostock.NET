# 上证50成分股

## 方法说明

查询上证50指数成分股列表。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_sz50_stocks()` | `client.QuerySz50StocksAsync()` |

## 方法签名

```csharp
public async IAsyncEnumerable<IndexConstituentRow> QuerySz50StocksAsync(
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

await foreach (var row in client.QuerySz50StocksAsync())
{
    Console.WriteLine($"{row.Code} {row.CodeName}");
}
```

## 数据范围

- 数据从 2015 年开始
- 成分股每半年调整一次（6月、12月）
