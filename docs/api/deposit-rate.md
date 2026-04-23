# 存款利率

## 方法说明

查询中国人民银行公布的存款基准利率数据。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_deposit_rate_data()` | `client.QueryDepositRateDataAsync()` |

## 方法签名

```csharp
public async IAsyncEnumerable<DepositRateRow> QueryDepositRateDataAsync(
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
| DemandDepositRate | string? | 活期存款利率（%） |
| FixedDepositRate3Month | string? | 定期存款利率-3个月（%） |
| FixedDepositRate6Month | string? | 定期存款利率-6个月（%） |
| FixedDepositRate1Year | string? | 定期存款利率-1年（%） |
| FixedDepositRate2Year | string? | 定期存款利率-2年（%） |
| FixedDepositRate3Year | string? | 定期存款利率-3年（%） |
| FixedDepositRate5Year | string? | 定期存款利率-5年（%） |
| InstallmentFixedDepositRate1Year | string? | 零存整取/整存零取/存本取息利率-1年（%） |
| InstallmentFixedDepositRate3Year | string? | 零存整取/整存零取/存本取息利率-3年（%） |
| InstallmentFixedDepositRate5Year | string? | 零存整取/整存零取/存本取息利率-5年（%） |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryDepositRateDataAsync(startDate: "2020-01-01"))
{
    Console.WriteLine($"{row.PubDate} 活期:{row.DemandDepositRate} 1年定期:{row.FixedDepositRate1Year}");
}
```

## 数据范围

- 数据从 1989 年开始
- 包含历次央行存款基准利率调整记录
