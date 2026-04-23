# 季频业绩预告

## 方法说明

查询上市公司季频业绩预告数据。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_forecast_report()` | `client.QueryForecastReportAsync()` |

## 方法签名

```csharp
public async IAsyncEnumerable<ForecastReportRow> QueryForecastReportAsync(
    string code,
    string? startDate = null,
    string? endDate = null,
    CancellationToken ct = default)
```

## 参数说明

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| code | string | 是 | 证券代码，如 `"sh.600000"` |
| startDate | string? | 否 | 开始日期，格式 `"yyyy-MM-dd"`，默认 `"2015-01-01"` |
| endDate | string? | 否 | 结束日期，格式 `"yyyy-MM-dd"`，默认当天 |
| ct | CancellationToken | 否 | 取消令牌 |

## 返回字段

| 字段 | 类型 | 说明 |
|------|------|------|
| Code | string | 证券代码 |
| ProfitForcastExpPubDate | string? | 业绩预告发布日期 |
| ProfitForcastExpStatDate | string? | 业绩预告统计日期 |
| ProfitForcastType | string? | 业绩预告类型（预增/预减/扭亏/首亏/续亏/续盈/略增/略减/不确定） |
| ProfitForcastAbstract | string? | 业绩预告摘要 |
| ProfitForcastChgPctUp | string? | 预告净利润变动幅度上限（%） |
| ProfitForcastChgPctDwn | string? | 预告净利润变动幅度下限（%） |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryForecastReportAsync("sh.600000", startDate: "2023-01-01"))
{
    Console.WriteLine($"{row.Code} {row.ProfitForcastExpStatDate} 类型:{row.ProfitForcastType} 上限:{row.ProfitForcastChgPctUp}%");
}
```

## 数据范围

- 数据从 2003 年开始
- 业绩预告为上市公司在定期报告披露前对可能出现的业绩变动进行预告
