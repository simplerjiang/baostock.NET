# 货币供应量（月度）

## 方法说明

查询货币供应量月度数据，包括 M0、M1、M2 及其同比、环比数据。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_money_supply_data_month()` | `client.QueryMoneySupplyDataMonthAsync()` |

## 方法签名

```csharp
public async IAsyncEnumerable<MoneySupplyMonthRow> QueryMoneySupplyDataMonthAsync(
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
| StatMonth | string? | 统计月份 |
| M0Month | string? | M0（流通中货币，亿元） |
| M0YOY | string? | M0 同比增长率（%） |
| M0ChainRelative | string? | M0 环比增长率（%） |
| M1Month | string? | M1（狭义货币，亿元） |
| M1YOY | string? | M1 同比增长率（%） |
| M1ChainRelative | string? | M1 环比增长率（%） |
| M2Month | string? | M2（广义货币，亿元） |
| M2YOY | string? | M2 同比增长率（%） |
| M2ChainRelative | string? | M2 环比增长率（%） |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryMoneySupplyDataMonthAsync(startDate: "2024-01-01"))
{
    Console.WriteLine($"{row.StatYear}-{row.StatMonth} M2:{row.M2Month}亿 同比:{row.M2YOY}%");
}
```

## 数据范围

- 数据从 1999 年开始
- 按月更新，一般在次月中旬发布
