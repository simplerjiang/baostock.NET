# 货币供应量（年底余额）

## 方法说明

查询货币供应量年底余额数据，包括 M0、M1、M2 年度余额及同比数据。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_money_supply_data_year()` | `client.QueryMoneySupplyDataYearAsync()` |

## 方法签名

```csharp
public async IAsyncEnumerable<MoneySupplyYearRow> QueryMoneySupplyDataYearAsync(
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
| StatYear | string? | 统计年份 |
| M0Year | string? | M0（流通中货币，年末余额，亿元） |
| M0YearYOY | string? | M0 同比增长率（%） |
| M1Year | string? | M1（狭义货币，年末余额，亿元） |
| M1YearYOY | string? | M1 同比增长率（%） |
| M2Year | string? | M2（广义货币，年末余额，亿元） |
| M2YearYOY | string? | M2 同比增长率（%） |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryMoneySupplyDataYearAsync(startDate: "2020-01-01"))
{
    Console.WriteLine($"{row.StatYear} M2年末:{row.M2Year}亿 同比:{row.M2YearYOY}%");
}
```

## 数据范围

- 数据从 1999 年开始
- 按年更新
