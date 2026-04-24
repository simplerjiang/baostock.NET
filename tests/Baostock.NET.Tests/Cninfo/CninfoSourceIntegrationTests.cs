using System.Text;
using Baostock.NET.Cninfo;

namespace Baostock.NET.Tests.Cninfo;

/// <summary>
/// <see cref="CninfoSource"/> 端到端集成测试（使用 <see cref="InMemoryCninfoServer"/> 本地 mock）。
/// </summary>
public class CninfoSourceIntegrationTests
{
    private const string SampleResponseJson = """
    {
      "totalAnnouncement": 2,
      "totalRecordNum": 2,
      "announcements": [
        {
          "announcementId": "1225114741",
          "secCode": "600519",
          "secName": "贵州茅台",
          "orgId": "gssh0600519",
          "announcementTitle": "贵州茅台2024年年度报告",
          "announcementTime": 1712678400000,
          "adjunctUrl": "finalpage/2026-04-17/1225114741.PDF",
          "adjunctSize": 1892,
          "adjunctType": "PDF",
          "columnId": "125_annualreportpro_content"
        },
        {
          "announcementId": "1225114999",
          "secCode": "600519",
          "secName": "贵州茅台",
          "orgId": "gssh0600519",
          "announcementTitle": "贵州茅台2024年半年度报告",
          "announcementTime": 1693526400000,
          "adjunctUrl": "finalpage/2024-08-31/1225114999.PDF",
          "adjunctType": "PDF",
          "columnId": "125_semiannualreport_content"
        }
      ]
    }
    """;

    [Fact]
    public async Task QueryAnnouncementsAsync_ReturnsParsedRows()
    {
        using var server = new InMemoryCninfoServer();
        server.Setup("POST", "/new/hisAnnouncement/query", SampleResponseJson);
        // N-03：改走线上 topSearch/query 获取真实 orgId，不再合成。
        server.Setup("POST", "/new/information/topSearch/query",
            """[{"code":"600519","orgId":"gssh0600519","zwjc":"贵州茅台"}]""");

        var source = new CninfoSource(baseUri: server.BaseUri, pdfBaseUri: server.BaseUri);
        var rows = await source.QueryAnnouncementsAsync(new CninfoAnnouncementRequest(
            Code: "SH600519",
            Category: CninfoAnnouncementCategory.All,
            PageNum: 1,
            PageSize: 30));

        Assert.Equal(2, rows.Count);
        Assert.Equal("1225114741", rows[0].AnnouncementId);
        Assert.Equal("SH600519", rows[0].Code);
        Assert.Equal("贵州茅台2024年年度报告", rows[0].Title);
        Assert.Equal("finalpage/2026-04-17/1225114741.PDF", rows[0].AdjunctUrl);
        Assert.Equal("1225114999", rows[1].AnnouncementId);

        // 验证：先打 topSearch 查 orgId，再打 hisAnnouncement；且表单含真实 orgId。
        var received = server.Received;
        Assert.Contains(received, r => r.Path == "/new/information/topSearch/query");
        var annReq = Assert.Single(received, r => r.Path == "/new/hisAnnouncement/query");
        Assert.Equal("POST", annReq.Method);
        var bodyText = Encoding.UTF8.GetString(annReq.Body);
        Assert.Contains("stock=600519%2Cgssh0600519", bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("column=sse", bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pageNum=1", bodyText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadPdfAsync_ReturnsStream()
    {
        var payload = CreateRandomBytes(1024, seed: 42);
        using var server = new InMemoryCninfoServer();
        server.Setup("GET", "/finalpage/2026-04-17/sample.PDF", payload);

        var source = new CninfoSource(baseUri: server.BaseUri, pdfBaseUri: server.BaseUri);
        await using var stream = await source.DownloadPdfAsync("finalpage/2026-04-17/sample.PDF");
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        Assert.Equal(payload, ms.ToArray());
    }

    [Fact]
    public async Task DownloadPdfToFileAsync_ResumeTrue_AppendsFromExistingSize()
    {
        var payload = CreateRandomBytes(1000, seed: 7);
        using var server = new InMemoryCninfoServer();
        server.Setup("GET", "/finalpage/2026-04-17/resume.PDF", payload);

        // 准备：目标文件已存在前 500 字节
        var tmp = Path.Combine(Path.GetTempPath(), $"cninfo_resume_{Guid.NewGuid():N}.pdf");
        try
        {
            await File.WriteAllBytesAsync(tmp, payload.AsSpan(0, 500).ToArray());

            var source = new CninfoSource(baseUri: server.BaseUri, pdfBaseUri: server.BaseUri);
            var total = await source.DownloadPdfToFileAsync(
                "finalpage/2026-04-17/resume.PDF", tmp, resume: true);

            Assert.Equal(1000L, total);
            var fileBytes = await File.ReadAllBytesAsync(tmp);
            Assert.Equal(1000, fileBytes.Length);
            Assert.Equal(payload, fileBytes);

            // 校验服务端确实收到了 Range 头
            var captured = Assert.Single(server.Received);
            Assert.True(captured.Headers.TryGetValue("Range", out var rangeVal), "Range header missing on resume download.");
            Assert.Equal("bytes=500-", rangeVal);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public async Task DownloadPdfToFileAsync_ResumeFalse_OverwritesFile()
    {
        var payload = CreateRandomBytes(1000, seed: 11);
        using var server = new InMemoryCninfoServer();
        server.Setup("GET", "/finalpage/2026-04-17/overwrite.PDF", payload);

        var tmp = Path.Combine(Path.GetTempPath(), $"cninfo_overwrite_{Guid.NewGuid():N}.pdf");
        try
        {
            // 预置 500 字节，与 payload 前缀不同，用以验证真正被覆盖
            var preset = new byte[500];
            for (int i = 0; i < preset.Length; i++) preset[i] = 0xAB;
            await File.WriteAllBytesAsync(tmp, preset);

            var source = new CninfoSource(baseUri: server.BaseUri, pdfBaseUri: server.BaseUri);
            var total = await source.DownloadPdfToFileAsync(
                "finalpage/2026-04-17/overwrite.PDF", tmp, resume: false);

            Assert.Equal(1000L, total);
            var fileBytes = await File.ReadAllBytesAsync(tmp);
            Assert.Equal(1000, fileBytes.Length);
            Assert.Equal(payload, fileBytes);

            var captured = Assert.Single(server.Received);
            Assert.False(captured.Headers.ContainsKey("Range"),
                "Range header must NOT be sent when resume=false.");
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    private static byte[] CreateRandomBytes(int length, int seed)
    {
        var buf = new byte[length];
        new Random(seed).NextBytes(buf);
        return buf;
    }
}
