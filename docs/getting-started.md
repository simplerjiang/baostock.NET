# 快速开始

## 安装

通过 NuGet 安装：

```shell
dotnet add package Baostock.NET
```

要求 .NET 9.0 或更高版本。

## 基本使用流程

Baostock.NET 的使用遵循 **login → query → process → logout** 的标准流程：

```csharp
using Baostock.NET.Client;

// 1. 创建客户端并登录（推荐使用 await using 自动登出）
await using var client = await BaostockClient.CreateAndLoginAsync();

// 2. 查询数据（v1.2.0 BREAKING：证券代码默认东方财富风格 SH600000 / SZ000001 / BJ430047，亦兼容 sh.600000、sh600000、600000.SH等格式）
await foreach (var row in client.QueryHistoryKDataPlusAsync("SH600000"))
{
    // 3. 处理每一行数据
    Console.WriteLine($"{row.Date} 收盘价: {row.Close}");
}
// 4. await using 结束时自动调用 DisposeAsync → LogoutAsync
```

## `await using` 模式

`BaostockClient` 实现了 `IAsyncDisposable`，**强烈推荐**使用 `await using` 模式：

```csharp
await using var client = await BaostockClient.CreateAndLoginAsync();
// ... 查询数据 ...
// 离开作用域时自动登出并释放 TCP 连接
```

等价于手动管理：

```csharp
var client = await BaostockClient.CreateAndLoginAsync();
try
{
    // ... 查询数据 ...
}
finally
{
    await client.DisposeAsync();
}
```

## IAsyncEnumerable 流式消费

所有 `Query*Async` 方法返回 `IAsyncEnumerable<T>`，自动处理分页，逐行流式返回：

```csharp
// 流式消费：内存中只保持当前行，适合大数据量
await foreach (var row in client.QueryAllStockAsync())
{
    Console.WriteLine($"{row.Code} {row.CodeName}");
}
```

如需一次拉取全部数据到内存，可用 LINQ 的 `ToListAsync`（需引用 `System.Linq.Async`）：

```csharp
// 一次拉完到 List，适合数据量不大的场景
var allStocks = await client.QueryAllStockAsync().ToListAsync();
Console.WriteLine($"共 {allStocks.Count} 只证券");
```

## 错误处理

服务端返回的业务错误会抛出 `BaostockException`：

```csharp
using Baostock.NET.Client;

try
{
    await using var client = await BaostockClient.CreateAndLoginAsync();
    await foreach (var row in client.QueryHistoryKDataPlusAsync("xx.000000"))
    {
        Console.WriteLine(row);
    }
}
catch (BaostockException ex)
{
    Console.WriteLine($"错误码: {ex.ErrorCode}, 消息: {ex.Message}");
}
```

`BaostockException` 包含：

| 属性 | 类型 | 说明 |
|------|------|------|
| ErrorCode | string | 服务端返回的错误码（如 `10004020`） |
| Message | string | 服务端返回的错误描述 |

## 匿名 vs 认证账号

默认使用匿名登录，无需注册即可使用全部查询功能：

```csharp
// 匿名登录（默认）
await using var client = await BaostockClient.CreateAndLoginAsync();

// 使用注册账号登录
await using var client2 = await BaostockClient.CreateAndLoginAsync("your_user_id", "your_password");
```

> baostock 服务端目前对匿名用户和注册用户返回相同的数据，注册账号主要用于统计和未来可能的权限控制。

## 取消操作

所有异步方法支持 `CancellationToken`：

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

await using var client = await BaostockClient.CreateAndLoginAsync(ct: cts.Token);
await foreach (var row in client.QueryHistoryKDataPlusAsync("SH600000", ct: cts.Token))
{
    Console.WriteLine(row.Close);
}
```
