# 除权除息信息

## 方法说明

查询除权除息信息，包括分红送股、配股等明细数据。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_dividend_data()` | `client.QueryDividendDataAsync()` |

## 方法签名

```csharp
public async IAsyncEnumerable<DividendRow> QueryDividendDataAsync(
    string code,
    string? year = null,
    string yearType = "report",
    CancellationToken ct = default)
```

## 参数说明

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| code | string | 是 | 证券代码，如 `"sh.600000"` |
| year | string? | 否 | 年份，如 `"2024"`，默认当前年 |
| yearType | string | 否 | 年份类型：`"report"` 按报告期，`"operate"` 按除权除息日期 |
| ct | CancellationToken | 否 | 取消令牌 |

## 返回字段

| 字段 | 类型 | 说明 |
|------|------|------|
| Code | string | 证券代码 |
| DividPreNoticeDate | string? | 预披露公告日 |
| DividAgmPumDate | string? | 股东大会公告日 |
| DividPlanAnnounceDate | string? | 预案公告日 |
| DividPlanDate | string? | 分红实施公告日 |
| DividRegistDate | string? | 股权登记日 |
| DividOperateDate | string? | 除权除息日 |
| DividPayDate | string? | 派息日 |
| DividStockMarketDate | string? | 红股上市日 |
| DividCashPsBeforeTax | string? | 每股税前派息（元） |
| DividCashPsAfterTax | string? | 每股税后派息（元） |
| DividStocksPs | string? | 每股送红股 |
| DividCashStock | string? | 分红送转 |
| DividReserveToStockPs | string? | 每股转增资本 |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryDividendDataAsync("sh.600000", year: "2023"))
{
    Console.WriteLine($"{row.Code} 除权日:{row.DividOperateDate} 每股派息:{row.DividCashPsBeforeTax}");
}
```

## 数据范围

- 数据从 2000 年开始
- 包含分红送股、转增股本等除权除息信息
