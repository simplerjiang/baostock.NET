> v1.2.0 专集 | 面向从 v1.0.x 升级的用户 | 2026-04-24

# 从 v1.0.x 升级到 v1.2.0

本文档为 v1.0.x 用户提供完整的升级路径,包含 4 条 BREAKING CHANGES 的前后对比与代码迁移示例。

## 升级前检查清单

在升级前,请确认你的项目是否用到以下 API —— 用到的越多,受影响面越大:

- 是否在业务代码里手写 `sh.600519` / `sz.000001` 格式的证券代码?
- 是否 catch 过 `FormatException` 来处理无效 code?
- 是否依赖 `row.Code`(如 `StockBasicRow.Code`)字符串做下游拼接、缓存 key、日志 tag?
- 是否使用 `client.IsLoggedIn` 判断连接状态?
- 是否实现过自定义 `ITransport`?

如果**全部没有**,升级只需改版本号,向后兼容层会吸收所有变化。

## BREAKING CHANGES

### BREAKING 1:证券代码格式

**变更**

- **v1.0.x**:`sh.600519` / `sz.000001`(baostock native,小写 + 点)
- **v1.2.0**:`SH600519` / `SZ000001`(东财风格,大写,无点)

**影响**:所有 API 的 code 参数、返回值 `row.Code` 字段。

**好消息**:`CodeFormatter` 输入端**向后兼容**,旧格式 `sh.600519` 仍然可直接传入 API;只是**返回值统一成新格式**。

**迁移**

```csharp
// v1.0.x
var rows = await client.QueryHistoryKDataPlusAsync("sh.600519", fields, "2024-01-01", "2024-12-31");

// v1.2.0(推荐:统一成新格式)
var rows = await client.QueryHistoryKDataPlusAsync("SH600519", fields, "2024-01-01", "2024-12-31");

// v1.2.0(兼容:旧格式也能工作)
var rows = await client.QueryHistoryKDataPlusAsync("sh.600519", fields, "2024-01-01", "2024-12-31");
// ↑ CodeFormatter 会自动识别 sh.600519 并转成 SH600519
```

支持的 6 种输入格式见[数据源文档第 6 节](./sources.md#6-codeformatter-支持的输入格式)。

---

### BREAKING 2:异常类型契约

**变更**

- **v1.0.x**:无效 code 直接抛 `FormatException`
- **v1.2.0**:统一抛 `ArgumentException`,原始 `FormatException` 挂在 `InnerException` 保留细节

**动机**:对齐 .NET BCL 惯例(参数校验用 `ArgumentException` / `ArgumentNullException`),同时不丢失原始错误。

**迁移**

```csharp
// v1.0.x
try
{
    var rows = await client.QueryHistoryKDataPlusAsync("invalid_code", ...);
}
catch (FormatException ex)
{
    logger.LogError(ex, "code format error");
}

// v1.2.0
try
{
    var rows = await client.QueryHistoryKDataPlusAsync("invalid_code", ...);
}
catch (ArgumentException ex)
{
    // 需要原始 FormatException 细节时
    var format = ex.InnerException as FormatException;
    logger.LogError(ex, "code format error: {Detail}", format?.Message);
}
```

---

### BREAKING 3:Models 输出格式

**变更**

- **v1.0.x**:`row.Code` 返回 baostock native 格式 `sh.600519`
- **v1.2.0**:`row.Code` 返回东财风格 `SH600519`

**影响**:所有 `*Row` 模型的 `Code` 字段(`StockBasicRow`、`KLineRow`、`StockListRow` 等)。

**迁移**

```csharp
// v1.0.x
var rows = await client.QueryStockBasicAsync();
foreach (var r in rows)
{
    var cacheKey = $"stock:{r.Code}"; // cacheKey = "stock:sh.600519"
    cache.Set(cacheKey, r);
}

// v1.2.0(什么都不用改,但 cacheKey 的值变了)
var rows = await client.QueryStockBasicAsync();
foreach (var r in rows)
{
    var cacheKey = $"stock:{r.Code}"; // cacheKey = "stock:SH600519"
    cache.Set(cacheKey, r);
}
```

**迁移陷阱**:如果你的缓存、数据库、日志里已经存了旧格式 code,升级后会出现"读不到旧缓存"的现象。需要:

1. 清空旧缓存,或
2. 业务层加 `CodeFormatter.Normalize()` 统一处理,或
3. 写一次性脚本把 `sh.XXXXXX` 批量迁移为 `SHXXXXXX`

---

### BREAKING 4(half):`IsLoggedIn` 语义

**变更**

- **v1.0.x**:`client.IsLoggedIn = Session.IsLoggedIn`(只看内存状态)
- **v1.2.0**:`client.IsLoggedIn = Session.IsLoggedIn && _transport.IsConnected`(同时看 socket 健康)
- **新增属性** `client.IsConnected`:单独暴露 socket 状态

**动机**:v1.0.x 里 socket 半死时 `IsLoggedIn` 仍返回 true,用户以为还连着但下一次调用就会报错。v1.2.0 把"真实可用"语义收进 `IsLoggedIn`。

**为什么是 half-breaking**:大部分用户其实想要新语义(更真实);只有少数依赖"纯内存态"的代码需要改。

**迁移**

```csharp
// v1.0.x
if (client.IsLoggedIn)
{
    // 可能 socket 已半死
    await client.QueryAllStockAsync();
}

// v1.2.0(默认:新语义,更安全)
if (client.IsLoggedIn)
{
    // 保证 session + socket 都健康
    await client.QueryAllStockAsync();
}

// v1.2.0(需要旧"纯内存态"语义时)
if (client.Session.IsLoggedIn)
{
    // Session.IsLoggedIn 保留旧语义
}

// v1.2.0(单独检查 socket)
if (client.IsConnected)
{
    // socket 健康但可能未 login
}
```

**自定义 `ITransport` 实现者注意**:`ITransport` 接口新增 `IsConnected` 属性,自定义实现必须补充。

## 推荐迁移步骤

1. **更新包版本**
   ```powershell
   dotnet add package Baostock.NET -v 1.2.0
   ```
2. **编译**:让编译器找出 breaking(主要是 `ITransport` 自定义实现、catch `FormatException` 不再匹配等)
3. **更新 catch 子句**:`FormatException` → `ArgumentException`
4. **测试 code 字符串输入**:旧代码里的 `sh.600519` 应仍可工作(向后兼容)
5. **检查 `IsLoggedIn` 使用处**:
   - 想要"真实可用"语义 → 不改
   - 需要旧纯内存态语义 → 换成 `client.Session.IsLoggedIn`
   - 只想知道 socket 是否健康 → 换成 `client.IsConnected`
6. **跑一次完整回归测试**(单元 + 集成)

## 新增 API(建议试用)

v1.2.0 新增的多源 HTTP API:

```csharp
// 单个实时行情(Sina/Tencent/EastMoney 三源对冲)
var quote = await client.GetRealtimeQuoteAsync("SH600519");

// 批量实时行情
var quotes = await client.GetRealtimeQuotesAsync(new[] { "SH600519", "SZ000001", "SZ300750" });

// 历史 K 线(EastMoney + Tencent 双源对冲)
var klines = await client.GetHistoryKLineAsync(
    code: "SH600519",
    frequency: KLineFrequency.Daily,
    start: "2024-01-01",
    end: "2024-12-31",
    adjust: AdjustFlag.Forward);
```

详见[数据源文档](./sources.md)。

## 回滚方案

如果升级后遇到阻塞问题,可以回退到 v1.0.1:

```powershell
dotnet add package Baostock.NET -v 1.0.1
```

v1.2.0 对存储格式 / 配置文件均**无破坏**,单纯回退包版本即可,不会留下任何数据层面的不兼容痕迹。
