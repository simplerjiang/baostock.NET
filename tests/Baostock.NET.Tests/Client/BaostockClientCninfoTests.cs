using Baostock.NET.Client;

namespace Baostock.NET.Tests.Client;

/// <summary>
/// BaostockClient 巨潮公告接线的最小 smoke 测试：仅验证参数校验。
/// 真实网络流程由 CninfoSource 集成测试覆盖。
/// </summary>
public sealed class BaostockClientCninfoTests
{
    [Fact]
    public async Task QueryAnnouncements_NullRequest_Throws()
    {
        await using var client = new BaostockClient(new FakeTransport());
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.QueryAnnouncementsAsync(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DownloadPdf_NullOrWhitespaceUrl_Throws(string? url)
    {
        await using var client = new BaostockClient(new FakeTransport());
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.DownloadPdfAsync(url!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DownloadPdfToFile_NullOrWhitespaceUrl_Throws(string? url)
    {
        await using var client = new BaostockClient(new FakeTransport());
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.DownloadPdfToFileAsync(url!, "dest.pdf"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DownloadPdfToFile_NullOrWhitespaceDest_Throws(string? dest)
    {
        await using var client = new BaostockClient(new FakeTransport());
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.DownloadPdfToFileAsync("foo/bar.pdf", dest!));
    }
}
