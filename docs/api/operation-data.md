# 季频营运能力

## 方法说明

查询季频营运能力数据，包括应收账款周转率、存货周转率等指标。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_operation_data()` | `client.QueryOperationDataAsync()` |

## 方法签名

```csharp
public IAsyncEnumerable<OperationRow> QueryOperationDataAsync(
    string code,
    int year,
    int quarter,
    CancellationToken ct = default)
```

## 参数说明

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| code | string | 是 | 证券代码，如 `"sh.600000"` |
| year | int | 是 | 年份，如 `2024` |
| quarter | int | 是 | 季度（1-4） |
| ct | CancellationToken | 否 | 取消令牌 |

## 返回字段

| 字段 | 类型 | 说明 |
|------|------|------|
| Code | string | 证券代码 |
| PubDate | string? | 公告日期 |
| StatDate | string? | 统计截止日期 |
| NrTurnRatio | decimal? | 应收账款周转率（次） |
| NrTurnDays | decimal? | 应收账款周转天数（天） |
| InvTurnRatio | decimal? | 存货周转率（次） |
| InvTurnDays | decimal? | 存货周转天数（天） |
| CaTurnRatio | decimal? | 流动资产周转率（次） |
| AssetTurnRatio | decimal? | 总资产周转率（次） |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryOperationDataAsync("sh.600000", 2024, 1))
{
    Console.WriteLine($"{row.Code} {row.StatDate} 应收周转率:{row.NrTurnRatio} 存货周转率:{row.InvTurnRatio}");
}
```

## 数据范围

- 数据从 2007 年 1 季度开始
- 按季度更新，一般在季报公布后更新
