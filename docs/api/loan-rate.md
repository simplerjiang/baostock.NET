# 贷款利率

## 方法说明

查询中国人民银行公布的贷款基准利率数据。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_loan_rate_data()` | `client.QueryLoanRateDataAsync()` |

## 方法签名

```csharp
public async IAsyncEnumerable<LoanRateRow> QueryLoanRateDataAsync(
    string? startDate = null,
    string? endDate = null,
    CancellationToken ct = default)
```

## 参数说明

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| startDate | string? | 否 | 开始日期，格式 `"yyyy-MM-dd"` |
| endDate | string? | 否 | 结束日期，格式 `"yyyy-MM-dd"` |
| ct | CancellationToken | 否 | 取消令牌 |

## 返回字段

| 字段 | 类型 | 说明 |
|------|------|------|
| PubDate | string? | 公布日期 |
| LoanRate6Month | string? | 贷款利率-6个月以内（含）（%） |
| LoanRate6MonthTo1Year | string? | 贷款利率-6个月至1年（含）（%） |
| LoanRate1YearTo3Year | string? | 贷款利率-1年至3年（含）（%） |
| LoanRate3YearTo5Year | string? | 贷款利率-3年至5年（含）（%） |
| LoanRateAbove5Year | string? | 贷款利率-5年以上（%） |
| MortgateRateBelow5Year | string? | 公积金贷款利率-5年以下（含）（%） |
| MortgateRateAbove5Year | string? | 公积金贷款利率-5年以上（%） |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryLoanRateDataAsync(startDate: "2020-01-01"))
{
    Console.WriteLine($"{row.PubDate} 1年内:{row.LoanRate6MonthTo1Year} 5年以上:{row.LoanRateAbove5Year}");
}
```

## 数据范围

- 数据从 1991 年开始
- 包含历次央行贷款基准利率调整记录
