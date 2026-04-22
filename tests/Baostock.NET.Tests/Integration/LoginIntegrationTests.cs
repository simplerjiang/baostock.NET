using Baostock.NET.Client;

namespace Baostock.NET.Tests.Integration;

/// <summary>
/// 联网集成测试。CI 默认通过 <c>--filter "Category!=Live"</c> 跳过；本地必跑。
/// </summary>
[Trait("Category", "Live")]
public class LoginIntegrationTests
{
    [Fact]
    public async Task Anonymous_Login_Logout_Roundtrip_Succeeds()
    {
        await using var client = new BaostockClient();

        var login = await client.LoginAsync();
        Assert.Equal("0", login.ErrorCode);
        Assert.True(client.Session.IsLoggedIn);
        Assert.Equal("anonymous", client.Session.UserId);

        await client.LogoutAsync();
        Assert.False(client.Session.IsLoggedIn);
    }
}
