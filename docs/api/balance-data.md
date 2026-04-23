# 季频偿债能力

## 方法说明

查询季频偿债能力数据，包括流动比率、速动比率、现金比率等指标。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_balance_data()` | `client.QueryBalanceDataAsync()` |

## 方法签名

```csharp
public IAsyncEnumerable<BalanceRow> QueryBalanceDataAsync(
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
| CurrentRatio | decimal? | 流动比率 |
| QuickRatio | decimal? | 速动比率 |
| CashRatio | decimal? | 现金比率 |
| YoyLiability | decimal? | 总负债同比增长率（%） |
| LiabilityToAsset | decimal? | 资产负债率（%） |
| AssetToEquity | decimal? | 权益乘数 |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryBalanceDataAsync("sh.600000", 2024, 1))
{
    Console.WriteLine($"{row.Code} {row.StatDate} 流动比率:{row.CurrentRatio} 速动比率:{row.QuickRatio}");
}
```

## 数据范围

- 数据从 2007 年 1 季度开始
- 按季度更新，一般在季报公布后更新
