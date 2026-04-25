using Baostock.NET.Cninfo;

namespace Baostock.NET.Tests.Cninfo;

/// <summary>
/// v1.3.3 (Bug-3) 回归：<see cref="CninfoSource.DownloadPdfAsync(string, long?, System.Threading.CancellationToken)"/>
/// 在传入 <c>rangeStart</c> 时必须把 Range 头透传到上游，并把上游 206 响应剩余字节如实返回。
/// </summary>
/// <remarks>
/// 这层是 v1.3.2 已经能跑通的下游契约（PDF 续传依赖），加单测固化下来，避免回归。
/// TestUI 端 <c>bytes=A-B</c> 切片逻辑无法在此测（TestUI 是独立进程），改用 README.UserAgentTest.md UR 步骤手动验收。
/// </remarks>
public class CninfoSourceRangeTests
{
    [Fact]
    public async Task DownloadPdfAsync_WithRangeStart_ReturnsRemainderOnly()
    {
        // 5000 字节的固定 payload；rangeStart=1000 期望剩余 4000 字节，且字节序列与 payload[1000..] 完全一致。
        var payload = CreateSeededBytes(5000, seed: 17);
        using var server = new InMemoryCninfoServer();
        server.Setup("GET", "/finalpage/2026-04-25/range.PDF", payload);

        var source = new CninfoSource(baseUri: server.BaseUri, pdfBaseUri: server.BaseUri);
        await using var stream = await source.DownloadPdfAsync(
            "finalpage/2026-04-25/range.PDF", rangeStart: 1000);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        var got = ms.ToArray();
        Assert.Equal(4000, got.Length);
        Assert.Equal(payload.AsSpan(1000).ToArray(), got);

        // 验证服务端确实收到了 Range: bytes=1000-。
        var captured = Assert.Single(server.Received);
        Assert.True(captured.Headers.TryGetValue("Range", out var rangeVal),
            "Range header missing on partial download.");
        Assert.Equal("bytes=1000-", rangeVal);
    }

    [Fact]
    public async Task DownloadPdfAsync_WithoutRangeStart_ReturnsFullPayload()
    {
        // 反向断言：不带 rangeStart 时，上游不应收到 Range 头，且响应为完整 payload。
        var payload = CreateSeededBytes(5000, seed: 23);
        using var server = new InMemoryCninfoServer();
        server.Setup("GET", "/finalpage/2026-04-25/full.PDF", payload);

        var source = new CninfoSource(baseUri: server.BaseUri, pdfBaseUri: server.BaseUri);
        await using var stream = await source.DownloadPdfAsync(
            "finalpage/2026-04-25/full.PDF");
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        Assert.Equal(payload, ms.ToArray());
        var captured = Assert.Single(server.Received);
        Assert.False(captured.Headers.ContainsKey("Range"),
            "Range header should not be sent when rangeStart is null.");
    }

    private static byte[] CreateSeededBytes(int length, int seed)
    {
        var buf = new byte[length];
        new Random(seed).NextBytes(buf);
        return buf;
    }
}
