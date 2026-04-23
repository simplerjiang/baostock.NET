# 证券基本资料

## 方法说明

查询证券基本资料，包括证券代码、名称、上市日期、退市日期、类型、状态等。支持按代码精确查询或按名称模糊查询。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.query_stock_basic()` | `client.QueryStockBasicAsync()` |

## 方法签名

```csharp
public async IAsyncEnumerable<StockBasicRow> QueryStockBasicAsync(
    string? code = null,
    string? codeName = null,
    CancellationToken ct = default)
```

## 参数说明

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| code | string? | 否 | 证券代码，如 `"sh.600000"`。为空时返回全部 |
| codeName | string? | 否 | 证券名称，支持模糊查询 |
| ct | CancellationToken | 否 | 取消令牌 |

> `code` 和 `codeName` 可同时为空（返回全部），也可同时指定（取交集）。

## 返回字段

| 字段 | 类型 | 说明 |
|------|------|------|
| Code | string | 证券代码 |
| CodeName | string | 证券名称 |
| IpoDate | string? | 上市日期 |
| OutDate | string? | 退市日期 |
| Type | string | 证券类型（1: 股票，2: 指数，3: 其他） |
| Status | string | 上市状态（1: 上市，0: 退市） |

## 使用示例

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();

// 按代码查询
await foreach (var row in client.QueryStockBasicAsync(code: "sh.600000"))
{
    Console.WriteLine($"{row.Code} {row.CodeName} 上市:{row.IpoDate} 类型:{row.Type}");
}

// 按名称模糊查询
await foreach (var row in client.QueryStockBasicAsync(codeName: "浦发"))
{
    Console.WriteLine($"{row.Code} {row.CodeName}");
}
```

## 数据范围

- 包含全部 A 股、指数等证券的基本信息
- 数据实时更新
