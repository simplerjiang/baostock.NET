> v1.3.0 专集 | 巨潮公告 + PDF 下载详解 | 2026-04-24

# 巨潮公告 + PDF 下载

v1.3.0 在 `BaostockClient` 上新增一组巨潮资讯网（`cninfo.com.cn`）公告能力：索引检索 + 流式 PDF 下载，后者支持 `Range` 断点续传。单源（不走 Hedged），失败直接抛 `DataSourceException`。

## 1. API 签名

位于 `Baostock.NET.Client.BaostockClient`（`BaostockClient.Cninfo.cs` 分部类）：

```csharp
using Baostock.NET.Cninfo;
using Baostock.NET.Models;

public Task<IReadOnlyList<CninfoAnnouncementRow>> QueryAnnouncementsAsync(
    CninfoAnnouncementRequest request,
    CancellationToken ct = default);

public Task<Stream> DownloadPdfAsync(
    string adjunctUrl,
    long? rangeStart = null,
    CancellationToken ct = default);

public Task<long> DownloadPdfToFileAsync(
    string adjunctUrl,
    string destinationPath,
    bool resume = false,
    CancellationToken ct = default);
```

### `CninfoAnnouncementRequest`

```csharp
public sealed record CninfoAnnouncementRequest(
    string Code,
    CninfoAnnouncementCategory Category = CninfoAnnouncementCategory.All,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    int PageNum = 1,
    int PageSize = 30);
```

### `CninfoAnnouncementRow`

| 字段 | 含义 |
|---|---|
| `AnnouncementId` | 巨潮内部公告 ID（字符串） |
| `Code` | 东财风格证券代码 |
| `SecurityName` | 证券简称 |
| `Title` | 公告标题 |
| `PublishDate` | 发布日期（`DateOnly`） |
| `Category` | 分类原文字符串（如 `"年报"` / `"半年报"`） |
| `AdjunctUrl` | PDF 相对路径（用于拼接下载 URL） |
| `FullPdfUrl` | 计算属性：`http://static.cninfo.com.cn/{AdjunctUrl}` |

## 2. `CninfoAnnouncementCategory` 枚举映射

后端将枚举映射到巨潮 `category` 请求参数（`CninfoSource.CategoryToParam`）：

| 枚举值 | 巨潮 `category` 参数 | 说明 |
|---|---|---|
| `AnnualReport` | `category_ndbg_szsh` | 年报 |
| `SemiAnnualReport` | `category_bndbg_szsh` | 半年报 |
| `QuarterlyReport` | `category_sjdbg_szsh` | 季报 |
| `PerformanceForecast` | `category_yjygjxz_szsh` | 业绩预告 |
| `TemporaryAnnouncement` | `category_lshgg_szsh` | 临时公告 |
| `All` | （空字符串） | 不按分类过滤 |

反查：调用方如需从返回 `Category` 字符串反推枚举，请自行维护映射（巨潮返回的 `Category` 文本与 `CategoryToParam` 的 key 并非 1:1 对应；推荐以调用时传入的枚举为准）。

## 3. 请求 / 响应示例

### 公告列表查询

**请求**：

```csharp
var req = new CninfoAnnouncementRequest(
    Code: "SH600519",
    Category: CninfoAnnouncementCategory.AnnualReport,
    StartDate: new DateOnly(2024, 1, 1),
    EndDate: null);
var rows = await client.QueryAnnouncementsAsync(req);
```

**底层 HTTP（供调试参考）**：

- Method：`POST`
- URL：`http://www.cninfo.com.cn/new/hisAnnouncement/query`
- Content-Type：`application/x-www-form-urlencoded`
- 关键表单字段：`stock=600519,gssh0600519` / `column=sse` / `category=category_ndbg_szsh` / `seDate=2024-01-01~` / `pageNum=1` / `pageSize=30`
- Referer：`http://www.cninfo.com.cn/new/commonUrl/pageOfSearch?url=disclosure/list/search`
- Origin：`http://www.cninfo.com.cn`
- `X-Requested-With: XMLHttpRequest`

**响应（截断）**：

```json
[
  {
    "announcementId": "1234567890",
    "code": "SH600519",
    "securityName": "贵州茅台",
    "title": "贵州茅台：2024 年年度报告",
    "publishDate": "2025-03-28",
    "category": "年报",
    "adjunctUrl": "finalpage/2025-03-28/1234567890.PDF",
    "fullPdfUrl": "http://static.cninfo.com.cn/finalpage/2025-03-28/1234567890.PDF"
  }
]
```

### PDF 流式下载

```csharp
// 方式 A：Stream，调用方自行 Dispose
await using var stream = await client.DownloadPdfAsync(rows[0].AdjunctUrl);
await stream.CopyToAsync(Console.OpenStandardOutput());

// 方式 B：直接落盘，resume=true 时按本地已存在文件大小续传
var bytes = await client.DownloadPdfToFileAsync(
    rows[0].AdjunctUrl,
    destinationPath: "./pdf/600519-2024-annual.pdf",
    resume: true);
Console.WriteLine($"written: {bytes} bytes");
```

## 4. Accept-Encoding 规避

`CninfoSource.BuildPdfHeaders()` 刻意 **不声明** `Accept-Encoding: br`，只允许默认的 `identity` / `gzip` / `deflate`：

- 原因：巨潮 CDN 曾观测到对 PDF 二进制资源也返回 `Content-Encoding: br`，但 body 并非有效 brotli 压缩流（可能是 CDN 配置错误），导致调用方读流时抛解码异常。
- 规避：不声明 brotli，CDN 就不会返回压缩 PDF。PDF 本身已是高度压缩的文件格式，禁用传输压缩对流量开销可以忽略。
- 相关字段：`HttpDataClient` 的 `SocketsHttpHandler` 全局声明了 `GZip | Deflate | Brotli`，但 PDF 下载走的是 `GetStreamAsync` 路径并显式覆盖 `Accept`/`Referer` 头。

## 5. Host 拆分（查询 vs PDF）

| 用途 | Host | 默认常量 | 调整 |
|---|---|---|---|
| 公告列表查询 | `www.cninfo.com.cn` | `CninfoSource.DefaultBaseUri` | 构造时传 `baseUri` 参数可重定向到本地 mock |
| PDF 静态资源 | `static.cninfo.com.cn` | `CninfoSource.DefaultPdfBaseUri` | 构造时传 `pdfBaseUri` 参数 |

拆分原因：查询接口是动态 API（`/new/hisAnnouncement/query`），PDF 走 CDN 静态资源，两者分属不同子域，防火墙策略也可能不同。

## 6. Range 断点续传协议

### `DownloadPdfAsync` 的 `rangeStart` 语义

```csharp
// 从头下载（不发送 Range 头）
await using var s1 = await client.DownloadPdfAsync(url, rangeStart: null);

// 从第 1024 字节开始下载（发送 Range: bytes=1024-）
await using var s2 = await client.DownloadPdfAsync(url, rangeStart: 1024);
```

服务端响应：

- 无 `Range` → `200 OK` + 完整 body
- 有 `Range` 且服务端支持 → `206 Partial Content` + 从 `rangeStart` 起的剩余字节
- 服务端不支持 → 多数 CDN 会降级为 `200 OK` + 完整 body（调用方需自行校验本地已有数据长度）

### `DownloadPdfToFileAsync` 的 `resume` 流程

```
┌──────────────────────────────────────────────────┐
│ 1. resume = true 且 destinationPath 已存在？     │
│    └─ 否 → rangeStart=null, FileMode.Create      │
│    └─ 是 → existing = FileInfo.Length            │
│            └─ existing > 0 →                      │
│                  rangeStart=existing              │
│                  FileMode.Append                  │
│            └─ existing == 0 → Create（同"否"）    │
│                                                   │
│ 2. 调用 DownloadPdfAsync(url, rangeStart)        │
│                                                   │
│ 3. 把返回 Stream 的内容 CopyTo FileStream        │
│    （Append 模式下写到文件末尾）                  │
│                                                   │
│ 4. 返回最终文件大小 new FileInfo(path).Length    │
└──────────────────────────────────────────────────┘
```

注意：`BaostockClient.DownloadPdfToFileAsync` 对外暴露的 `resume` **默认值是 `false`**（保守策略），而底层 `CninfoSource.DownloadPdfToFileAsync` 的 `resume` 默认值是 `true`。需要断点续传时请显式传 `resume: true`。

### 健康统计与失败

- 查询和下载都记录到 `SourceHealthRegistry.Default`，source 名固定为 `"Cninfo"`。
- 查询失败 → `DataSourceException("Cninfo", "Cninfo announcement query failed: {inner}", innerException)`
- PDF 失败 → `DataSourceException("Cninfo", "Cninfo PDF download failed: {inner}", innerException)`
- `ArgumentException`：`adjunctUrl` 为 `null` / 空 / 空白时抛出。

## 7. 已知限制

- **302 重定向不自动 follow**：极少数老公告的 `adjunctUrl` 会返回 302 到另一 CDN 域，当前 `HttpDataClient` 对 PDF 下载路径保持默认 redirect 行为；若遇到失败建议调用方捕获 `DataSourceException` 后改用浏览器直连。
- **空 `adjunctUrl`**：巨潮老数据偶见 `adjunctUrl` 为空字符串，调用方需过滤 `string.IsNullOrWhiteSpace(row.AdjunctUrl)`。
- **BJ 北交所 `column=bj`**：`CninfoSource.CodeToColumn` 已支持 `SH` → `sse` / `SZ` → `szse` / `BJ` → `bj`，但 BJ 公告在巨潮的覆盖度弱于沪深两市，检索可能返回 0 行，属预期。
- **单源无对冲**：与财报三表不同，公告 / PDF 走单源。一旦巨潮故障或改版，直接抛 `DataSourceException`；后续版本视情接入备用源（深交所 / 上交所 / 东财公告频道）。

## 8. 代码位置

- API 入口：[`src/Baostock.NET/Client/BaostockClient.Cninfo.cs`](../../src/Baostock.NET/Client/BaostockClient.Cninfo.cs)
- 源实现：[`src/Baostock.NET/Cninfo/CninfoSource.cs`](../../src/Baostock.NET/Cninfo/CninfoSource.cs) / [`ICninfoSource.cs`](../../src/Baostock.NET/Cninfo/ICninfoSource.cs)
- 请求 / 枚举：[`CninfoAnnouncementRequest.cs`](../../src/Baostock.NET/Cninfo/CninfoAnnouncementRequest.cs) / [`CninfoAnnouncementCategory.cs`](../../src/Baostock.NET/Cninfo/CninfoAnnouncementCategory.cs)
- Row 模型：[`src/Baostock.NET/Models/CninfoAnnouncementRow.cs`](../../src/Baostock.NET/Models/CninfoAnnouncementRow.cs)
- 代码格式化辅助：`CodeFormatter.CninfoOrgId` / `CninfoStock` 扩展（`gss{h|z|b}0{6位代码}` 风格）
