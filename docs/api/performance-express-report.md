# 季频业绩快报

## 方法说明

查询上市公司季频业绩快报数据。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_performance_express_report()` | `client.QueryPerformanceExpressReportAsync()` |

## 方法签名

```csharp
public async IAsyncEnumerable<PerformanceExpressRow> QueryPerformanceExpressReportAsync(
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
| PerformanceExpPubDate | string? | 业绩快报公告日期 |
| PerformanceExpStatDate | string? | 业绩快报统计日期 |
| PerformanceExpUpdateDate | string? | 业绩快报更新日期 |
| PerformanceExpressTotalAsset | string? | 总资产（元） |
| PerformanceExpressNetAsset | string? | 净资产（元） |
| PerformanceExpressEPSChgPct | string? | 每股收益变动幅度（%） |
| PerformanceExpressROEWa | string? | 净资产收益率（加权平均）（%） |
| PerformanceExpressEPSDiluted | string? | 每股收益（摊薄）（元） |
| PerformanceExpressGRYOY | string? | 营业总收入同比增长率（%） |
| PerformanceExpressOPYOY | string? | 营业利润同比增长率（%） |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryPerformanceExpressReportAsync("sh.600000", startDate: "2023-01-01"))
{
    Console.WriteLine($"{row.Code} {row.PerformanceExpStatDate} ROE:{row.PerformanceExpressROEWa}");
}
```

## 数据范围

- 数据从 2006 年开始
- 业绩快报为上市公司在定期报告之前发布的初步财务数据
