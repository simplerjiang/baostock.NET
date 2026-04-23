# 登录登出

## 方法说明

管理与 baostock 服务端的会话连接。登录后方可使用各查询 API，登出后释放连接。

## Python 对照

| Python | .NET |
|--------|------|
| `bs.login(user_id, password)` | `BaostockClient.CreateAndLoginAsync(userId, password)` |
| `bs.logout()` | `client.DisposeAsync()`（自动登出） |
| `bs.set_API_key(apiKey)` | `client.Session.ApiKey = apiKey` |

## 方法签名

```csharp
// 推荐：一站式创建 + 登录
public static Task<BaostockClient> CreateAndLoginAsync(
    string userId = "anonymous",
    string password = "123456",
    CancellationToken ct = default)

// 手动登录
public Task<LoginResult> LoginAsync(
    string userId = "anonymous",
    string password = "123456",
    CancellationToken ct = default)
```

## 参数说明

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| userId | string | 否 | 用户名，默认 `"anonymous"` |
| password | string | 否 | 密码，默认 `"123456"` |
| ct | CancellationToken | 否 | 取消令牌 |

## 返回值

`LoginResult` record：

| 字段 | 类型 | 说明 |
|------|------|------|
| ErrorCode | string | 错误码，成功时为 `"0"` |
| ErrorMessage | string | 错误信息，成功时为 `"success"` |
| Method | string? | 服务端回填的方法名 |
| UserId | string? | 服务端分配的会话 user_id |

## 使用示例

```csharp
using Baostock.NET.Client;

// 推荐方式：await using 自动管理生命周期
await using var client = await BaostockClient.CreateAndLoginAsync();

// 查询数据...
await foreach (var row in client.QueryTradeDatesAsync())
{
    Console.WriteLine($"{row.Date} 交易日: {row.IsTrading}");
}
// 离开作用域自动登出
```

## 说明

- `CreateAndLoginAsync` 是推荐入口，自动完成 TCP 连接和登录，失败时自动释放资源
- 已登录时重复调用 `LoginAsync` 为幂等操作，直接返回缓存结果
- `DisposeAsync` 会自动调用登出并关闭 TCP 连接
