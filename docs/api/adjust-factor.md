# 复权因子信息

## 方法说明

查询复权因子数据，用于计算前复权和后复权价格。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_adjust_factor()` | `client.QueryAdjustFactorAsync()` |

## 方法签名

```csharp
public async IAsyncEnumerable<AdjustFactorRow> QueryAdjustFactorAsync(
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
| DividOperateDate | string? | 除权除息日 |
| ForeAdjustFactor | decimal? | 前复权因子 |
| BackAdjustFactor | decimal? | 后复权因子 |
| AdjustFactor | decimal? | 本次复权因子 |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryAdjustFactorAsync("sh.600000", startDate: "2023-01-01"))
{
    Console.WriteLine($"{row.Code} {row.DividOperateDate} 前复权因子:{row.ForeAdjustFactor}");
}
```

## 数据范围

- 数据从 1990 年开始
- 包含每次除权除息对应的复权因子
