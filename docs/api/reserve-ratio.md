# 存款准备金率

## 方法说明

查询中国人民银行公布的法定存款准备金率数据。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_required_reserve_ratio_data()` | `client.QueryRequiredReserveRatioDataAsync()` |

## 方法签名

```csharp
public async IAsyncEnumerable<ReserveRatioRow> QueryRequiredReserveRatioDataAsync(
    string? startDate = null,
    string? endDate = null,
    string yearType = "0",
    CancellationToken ct = default)
```

## 参数说明

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| startDate | string? | 否 | 开始日期，格式 `"yyyy-MM-dd"` |
| endDate | string? | 否 | 结束日期，格式 `"yyyy-MM-dd"` |
| yearType | string | 否 | 年份类型，默认 `"0"` |
| ct | CancellationToken | 否 | 取消令牌 |

## 返回字段

| 字段 | 类型 | 说明 |
|------|------|------|
| PubDate | string? | 公布日期 |
| EffectiveDate | string? | 生效日期 |
| BigInstitutionsRatioPre | string? | 大型金融机构-调整前准备金率（%） |
| BigInstitutionsRatioAfter | string? | 大型金融机构-调整后准备金率（%） |
| MediumInstitutionsRatioPre | string? | 中小型金融机构-调整前准备金率（%） |
| MediumInstitutionsRatioAfter | string? | 中小型金融机构-调整后准备金率（%） |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

await foreach (var row in client.QueryRequiredReserveRatioDataAsync(startDate: "2020-01-01"))
{
    Console.WriteLine($"{row.PubDate} 生效:{row.EffectiveDate} 大型机构:{row.BigInstitutionsRatioAfter}%");
}
```

## 数据范围

- 数据从 1985 年开始
- 包含历次央行存款准备金率调整记录
