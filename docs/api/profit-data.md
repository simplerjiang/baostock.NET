# 季频盈利能力

## 方法说明

查询季频盈利能力数据，包括 ROE、净利率、毛利率等指标。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_profit_data()` | `client.QueryProfitDataAsync()` |

## 方法签名

```csharp
public IAsyncEnumerable<ProfitRow> QueryProfitDataAsync(
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
| RoeAvg | decimal? | 净资产收益率（平均）（%） |
| NpMargin | decimal? | 销售净利率（%） |
| GpMargin | decimal? | 销售毛利率（%） |
| NetProfit | decimal? | 净利润（元） |
| EpsTtm | decimal? | 每股收益（TTM） |
| MbRevenue | decimal? | 主营业务收入（元） |
| TotalShare | decimal? | 总股本 |
| LiqaShare | decimal? | 流通股本 |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryProfitDataAsync("sh.600000", 2024, 1))
{
    Console.WriteLine($"{row.Code} {row.StatDate} ROE:{row.RoeAvg} 净利润:{row.NetProfit}");
}
```

## 数据范围

- 数据从 2007 年 1 季度开始
- 按季度更新，一般在季报公布后更新
