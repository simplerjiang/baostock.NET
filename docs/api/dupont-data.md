# 季频杜邦指数

## 方法说明

查询季频杜邦分析指标，用于分析企业 ROE 的驱动因素分解。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_dupont_data()` | `client.QueryDupontDataAsync()` |

## 方法签名

```csharp
public IAsyncEnumerable<DupontRow> QueryDupontDataAsync(
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
| DupontRoe | decimal? | 净资产收益率（ROE） |
| DupontAssetStoEquity | decimal? | 权益乘数（总资产/净资产） |
| DupontAssetTurn | decimal? | 总资产周转率 |
| DupontPnitoni | decimal? | 归属母公司净利润/净利润 |
| DupontNitogr | decimal? | 净利润/营业总收入 |
| DupontTaxBurden | decimal? | 税负比率（净利润/利润总额） |
| DupontIntburden | decimal? | 利息负担率（利润总额/息税前利润） |
| DupontEbittogr | decimal? | 息税前利润/营业总收入 |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryDupontDataAsync("sh.600000", 2024, 1))
{
    Console.WriteLine($"{row.Code} {row.StatDate} ROE:{row.DupontRoe} 权益乘数:{row.DupontAssetStoEquity}");
}
```

## 数据范围

- 数据从 2007 年 1 季度开始
- 按季度更新，一般在季报公布后更新
