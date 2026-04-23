# 行业分类

## 方法说明

查询证券所属行业分类信息。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_stock_industry()` | `client.QueryStockIndustryAsync()` |

## 方法签名

```csharp
public async IAsyncEnumerable<StockIndustryRow> QueryStockIndustryAsync(
    string? code = null,
    string? date = null,
    CancellationToken ct = default)
```

## 参数说明

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| code | string? | 否 | 证券代码，如 `"sh.600000"`。为空时返回全部 |
| date | string? | 否 | 查询日期，格式 `"yyyy-MM-dd"`。为空时返回最新 |
| ct | CancellationToken | 否 | 取消令牌 |

## 返回字段

| 字段 | 类型 | 说明 |
|------|------|------|
| UpdateDate | string | 更新日期 |
| Code | string | 证券代码 |
| CodeName | string | 证券名称 |
| Industry | string | 所属行业 |
| IndustryClassification | string | 行业分类标准（如"申万一级行业"） |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

// 查询单只股票的行业
await foreach (var row in client.QueryStockIndustryAsync(code: "sh.600000"))
{
    Console.WriteLine($"{row.Code} {row.CodeName} 行业:{row.Industry} 分类:{row.IndustryClassification}");
}
```

## 数据范围

- 包含全部 A 股的行业分类信息
- 分类标准跟随证监会/申万等行业分类更新
