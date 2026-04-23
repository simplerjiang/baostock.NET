# 季频成长能力

## 方法说明

查询季频成长能力数据，包括净资产同比增长率、净利润同比增长率等指标。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_growth_data()` | `client.QueryGrowthDataAsync()` |

## 方法签名

```csharp
public IAsyncEnumerable<GrowthRow> QueryGrowthDataAsync(
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
| YoyEquity | decimal? | 净资产同比增长率（%） |
| YoyAsset | decimal? | 总资产同比增长率（%） |
| YoyNi | decimal? | 净利润同比增长率（%） |
| YoyEpsBasic | decimal? | 基本每股收益同比增长率（%） |
| YoyPni | decimal? | 归属母公司股东净利润同比增长率（%） |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryGrowthDataAsync("sh.600000", 2024, 1))
{
    Console.WriteLine($"{row.Code} {row.StatDate} 净利润同比:{row.YoyNi}% 净资产同比:{row.YoyEquity}%");
}
```

## 数据范围

- 数据从 2007 年 1 季度开始
- 按季度更新，一般在季报公布后更新
