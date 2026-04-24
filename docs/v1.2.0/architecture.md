> v1.2.0 专集 | 面向从 v1.0.x 升级的用户 | 2026-04-24

# v1.2.0 架构设计

本文档描述 Baostock.NET v1.2.0 的运行时架构、关键组件及其设计决策。核心变化是:**从纯 TCP 单源协议客户端,升级为 TCP + HTTP 多源并发的综合数据 SDK**。

## 1. 总览

### v1.0.x:纯 TCP 单源

```
┌──────────────────┐
│ BaostockClient   │
└────────┬─────────┘
         │ baostock 私有 TCP 协议
         ▼
┌──────────────────┐
│ api.baostock.com │
└──────────────────┘
```

### v1.2.0:TCP + HTTP 多源

```
┌──────────────────────────────────────────────────────────────┐
│                      BaostockClient                          │
│  ┌────────────────────────┐    ┌──────────────────────────┐  │
│  │ TCP 通道 (原 baostock) │    │   HTTP 多源通道 (新增)   │  │
│  │ - TcpTransport         │    │ - HedgedRequestRunner    │  │
│  │ - FrameCodec           │    │ - SourceHealthRegistry   │  │
│  │ - ResponseParser       │    │ - HttpDataClient         │  │
│  │ - Auto Reconnect+      │    │ - RetryPolicy            │  │
│  │   Relogin              │    │                          │  │
│  └────────────┬───────────┘    └─────────┬────────────────┘  │
└───────────────┼────────────────────────────┼─────────────────┘
                │                            │
                ▼                            ▼
       api.baostock.com         Sina / Tencent / EastMoney
```

TCP 通道仍然负责原 baostock 的登录、查询、分页;HTTP 通道负责实时行情与历史 K 线的多源对冲。两条通道在 `BaostockClient` 内部协同,对外呈现统一 API。

## 2. Hedged Requests(并发对冲请求)

### 执行模型

1. **t=0**:启动 P=0(最高优先级)源请求
2. **t=500 ms**:若 P=0 仍未返回,启动 P=1 源请求(并不取消 P=0)
3. **t=1000 ms**:若仍未返回,启动 P=2 源请求
4. **首个成功返回即胜出**(first success wins);其余请求进入 **2 秒宽限取消窗口**,超时强制 cancel
5. 所有源失败 → 抛 `AllSourcesFailedException`,聚合各源的 `DataSourceException`

### 配置参数

- `hedgeInterval`:默认 500 ms,构造函数可调
- 单源超时:由 `HttpDataClient` / `RetryPolicy` 控制
- 宽限窗口:2 s(硬编码,保证网络慢的 loser 能干净退出)

### 胜者语义

- **First success wins**:第一个返回成功响应的源获胜,其余直接取消
- 失败不算"完成",失败的源不会触发胜出;必须业务层成功解析才算
- 这保证了 hedge 对"慢源返回错误"也是免疫的

### 代码位置

- [HedgedRequestRunner.cs](../../src/Baostock.NET/Http/HedgedRequestRunner.cs)

## 3. SourceHealthRegistry(健康感知)

防止已故障的源一直占用 P=0 位置浪费 hedge 延迟。

### 核心规则

- **失败阈值**:3 次连续失败 → 标记源为不健康
- **冷却时间**:30 秒;冷却期内该源跳过,自动恢复后计数清零
- **成功重置**:任何一次成功直接清零失败计数

### 线程安全

- `ConcurrentDictionary<string, HealthState>` 持有各源状态
- 每个源内部用 per-source lock 保护 transition(避免并发 report 导致计数错乱)
- 全局单例:`SourceHealthRegistry.Default`,跨 `BaostockClient` 实例共享

### 代码位置

- [SourceHealthRegistry.cs](../../src/Baostock.NET/Http/SourceHealthRegistry.cs)

## 4. HttpDataClient(单例 HTTP 客户端)

封装 `SocketsHttpHandler`,解决 .NET HttpClient 常见坑(DNS 刷新、连接池耗尽、编码兼容)。

### SocketsHttpHandler 配置

| 参数 | 值 | 目的 |
|---|---|---|
| `PooledConnectionLifetime` | 5 min | 强制 DNS 刷新,避免 CDN 漂移后连错节点 |
| `PooledConnectionIdleTimeout` | 2 min | 回收空闲连接 |
| `MaxConnectionsPerServer` | 16 | 支撑并发对冲 + 批量查询 |
| `AutomaticDecompression` | GZip \| Deflate \| Brotli | 覆盖所有主流压缩 |

### 其它默认

- **默认 UA**:`Baostock.NET/{version}`(运行时反射读取程序集版本)
- **编码注册**:静态构造里 `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`,使 `GB18030` / `GBK` 在 .NET Core/.NET 5+ 下可用(Sina/Tencent 必需)

### 主要方法

- `SendAsync(HttpRequestMessage, ct)` — 底层通道
- `GetStringAsync(url, headers?, encoding?, timeout?, ct)` — 便捷 GET,支持自定义编码(关键:Sina/Tencent 返回 GBK)
- `PostFormAsync(url, form, headers?, ct)` — POST 表单(预留给未来扩展)

## 5. RetryPolicy(指数退避重试)

### 策略

- **初始延迟** → 翻倍 → **1 分钟封顶**
- 重试条件:`HttpRequestException | IOException` → 重试
- **不重试**:`OperationCanceledException`(尊重上层取消)

### 已知限制(v1.2.1 将修)

当前版本在 `CancellationToken` 被触发但底层抛出的是 `TaskCanceledException`(继承自 `OperationCanceledException`)时行为正确;但在极少数 `HttpClient` timeout 内部转成 `TaskCanceledException` 的场景下,会被错误识别为"用户取消"而不重试。v1.2.1 将通过检查 `ct.IsCancellationRequested` 区分两种语义。

## 6. 异常层级

```
DataSourceException          ← 单源失败(SourceName, StatusCode?)
    │
    ▼ (多源全挂时聚合)
AllSourcesFailedException    ← DataKind + 所有 inner DataSourceException
```

- `DataSourceException`:包含 `SourceName`、可选 `StatusCode`、原始响应 body 片段
- `AllSourcesFailedException`:`DataKind`(Quote / KLine / ...),`InnerExceptions` 聚合所有源异常;调试时可逐条查看

业务代码推荐:`try { ... } catch (AllSourcesFailedException ex) { logger.LogError(ex, "..."); }`。

## 7. TCP 自愈(v1.2.0-preview5 Sprint 3 P0)

长连接 baostock TCP 通道在生产环境会遇到"半死 socket"问题(对端 FIN 但本端还不知道),v1.2.0 引入完整的自愈机制。

### 半死检测

```csharp
// TcpTransport.IsConnected
bool isHalfDead = socket.Poll(0, SelectRead) && socket.Available == 0;
return socket.Connected && !isHalfDead;
```

- `Poll(0, SelectRead)` 返回 true 表示 socket 可读
- `Available == 0` 表示没有数据可读
- 二者同时满足 → 对端已关闭,本端 socket 已半死

`ITransport.IsConnected` 是 v1.2.0 新增属性(**BREAKING**:自定义 Transport 实现需补充)。

### BaostockClient.ReconnectAndReloginAsync

- **CAS lock**:`Interlocked.CompareExchange(ref _reconnectInProgress, 1, 0)` 保证同一时刻只有一个 reconnect 在跑
- **凭据缓存**:登录成功时记 `(UserId, Password)`,reconnect 后自动 relogin(v1.3.0 计划升级为 `SecureString`)
- **100 ms 自旋等待**:其它线程看到正在 reconnect 时短暂等待,最多 1 次重试后返回最新状态
- 失败抛 `BaostockException`,保留底层 inner exception

### IsLoggedIn 语义(half-BREAKING)

- **v1.0.x**:`IsLoggedIn = Session.IsLoggedIn`(只看内存态)
- **v1.2.0**:`IsLoggedIn = Session.IsLoggedIn && _transport.IsConnected`(同时看 socket 健康)
- **新增属性** `IsConnected`:单独暴露 socket 健康,供需要旧语义的代码使用
- 迁移细节见[迁移指南 BREAKING 4](./migration-from-1.0.md#breaking-4half-isloggedin-语义)

### 代码位置

- [BaostockClient.cs](../../src/Baostock.NET/Client/BaostockClient.cs)
- [TcpTransport.cs](../../src/Baostock.NET/Protocol/TcpTransport.cs)
- [ITransport.cs](../../src/Baostock.NET/Protocol/ITransport.cs)
