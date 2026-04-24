using Baostock.NET.Cninfo;
using Baostock.NET.Http;

namespace Baostock.NET.Tests.Cninfo;

/// <summary>
/// <see cref="CninfoOrgIdResolver"/> 在线解析器的端到端测试（<see cref="InMemoryCninfoServer"/> mock）。
/// </summary>
public class CninfoOrgIdResolverTests
{
    [Fact]
    public async Task Resolve_SHStock_ReturnsOrgIdFromApi()
    {
        using var server = new InMemoryCninfoServer();
        server.Setup("POST", "/new/information/topSearch/query",
            """[{"code":"600519","orgId":"gssh0600519","zwjc":"贵州茅台"}]""");

        var resolver = new CninfoOrgIdResolver(baseUri: server.BaseUri);
        var orgId = await resolver.ResolveAsync("SH600519");

        Assert.Equal("gssh0600519", orgId);
    }

    [Fact]
    public async Task Resolve_ChiNextStock_ReturnsRealOrgIdNotSynthesized()
    {
        using var server = new InMemoryCninfoServer();
        // 创业板真实 orgId 形如 "GD165627"，若 fallback 到合成会得 "gssz0300750" → 老 bug
        server.Setup("POST", "/new/information/topSearch/query",
            """[{"code":"300750","orgId":"GD165627","zwjc":"宁德时代"}]""");

        var resolver = new CninfoOrgIdResolver(baseUri: server.BaseUri);
        var orgId = await resolver.ResolveAsync("SZ300750");

        Assert.Equal("GD165627", orgId);
    }

    [Fact]
    public async Task Resolve_CodeNotFound_ThrowsDataSourceException()
    {
        using var server = new InMemoryCninfoServer();
        server.Setup("POST", "/new/information/topSearch/query", "[]");

        var resolver = new CninfoOrgIdResolver(baseUri: server.BaseUri);
        var ex = await Assert.ThrowsAsync<DataSourceException>(
            () => resolver.ResolveAsync("SZ999999"));
        Assert.Equal("Cninfo", ex.SourceName);
        Assert.Contains("999999", ex.Message);
    }

    [Fact]
    public async Task Resolve_CachesResultWithinProcess()
    {
        using var server = new InMemoryCninfoServer();
        server.Setup("POST", "/new/information/topSearch/query",
            """[{"code":"000001","orgId":"gssz0000001","zwjc":"平安银行"}]""");

        var resolver = new CninfoOrgIdResolver(baseUri: server.BaseUri);
        var first = await resolver.ResolveAsync("SZ000001");
        var second = await resolver.ResolveAsync("000001");

        Assert.Equal("gssz0000001", first);
        Assert.Equal("gssz0000001", second);
        // 第二次调用应命中缓存，服务器只应收到 1 次 topSearch 请求。
        var topSearchCalls = 0;
        foreach (var r in server.Received)
        {
            if (r.Path == "/new/information/topSearch/query") topSearchCalls++;
        }
        Assert.Equal(1, topSearchCalls);
    }

    [Fact]
    public void ParseOrgId_MatchingCode_ReturnsOrgId()
    {
        const string json = """
        [
          {"code":"000001","orgId":"gssz0000001","zwjc":"平安银行"},
          {"code":"000002","orgId":"gssz0000002","zwjc":"万科A"}
        ]
        """;
        Assert.Equal("gssz0000002", CninfoOrgIdResolver.ParseOrgId(json, "000002"));
    }

    [Fact]
    public void ParseOrgId_NoMatch_ReturnsNull()
    {
        Assert.Null(CninfoOrgIdResolver.ParseOrgId("[]", "600519"));
        Assert.Null(CninfoOrgIdResolver.ParseOrgId(
            """[{"code":"000001","orgId":"gssz0000001"}]""", "600519"));
    }

    [Theory]
    [InlineData("SH600519", "600519")]
    [InlineData("sh.600519", "600519")]
    [InlineData("600519", "600519")]
    [InlineData("sz000001", "000001")]
    [InlineData("BJ430047", "430047")]
    public void Normalize_VariousForms_ReturnsSixDigits(string input, string expected)
    {
        Assert.Equal(expected, CninfoOrgIdResolver.Normalize(input));
    }
}
