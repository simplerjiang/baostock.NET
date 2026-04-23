# 季频现金流量

## 方法说明

查询季频现金流量数据，包括现金流量相关指标。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_cash_flow_data()` | `client.QueryCashFlowDataAsync()` |

## 方法签名

```csharp
public IAsyncEnumerable<CashFlowRow> QueryCashFlowDataAsync(
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
| CaToAsset | decimal? | 流动资产除以总资产 |
| NcaToAsset | decimal? | 非流动资产除以总资产 |
| TangibleAssetToAsset | decimal? | 有形资产除以总资产 |
| EbitToInterest | decimal? | 已获利息倍数（EBIT/利息费用） |
| CfoToOr | decimal? | 经营活动产生的现金流量净额除以营业收入 |
| CfoToNp | decimal? | 经营活动产生的现金流量净额除以净利润 |
| CfoToGr | decimal? | 经营活动产生的现金流量净额除以营业总收入 |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryCashFlowDataAsync("sh.600000", 2024, 1))
{
    Console.WriteLine($"{row.Code} {row.StatDate} 现金/营收:{row.CfoToOr} 现金/净利:{row.CfoToNp}");
}
```

## 数据范围

- 数据从 2007 年 1 季度开始
- 按季度更新，一般在季报公布后更新
