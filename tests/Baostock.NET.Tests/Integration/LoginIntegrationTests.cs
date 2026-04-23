using Baostock.NET.Client;

namespace Baostock.NET.Tests.Integration;

/// <summary>
/// 联网集成测试。CI 默认通过 <c>--filter "Category!=Live"</c> 跳过；本地必跑。
/// 通过 <c>[Collection("Live")]</c> 与其他 Live 测试共享会话、串行执行。
/// </summary>
[Collection("Live")]
[Trait("Category", "Live")]
public class LoginIntegrationTests
{
    private readonly LiveTestFixture _fixture;

    public LoginIntegrationTests(LiveTestFixture fixture) => _fixture = fixture;

    [Fact]
    public void Fixture_Client_Is_LoggedIn()
    {
        Assert.True(_fixture.Client.Session.IsLoggedIn);
        Assert.Equal("anonymous", _fixture.Client.Session.UserId);
    }
}
