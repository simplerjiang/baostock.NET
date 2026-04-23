using Baostock.NET.Client;
using Xunit;

namespace Baostock.NET.Tests.Integration;

/// <summary>
/// 联网集成测试：用真实账号登录。凭据仅通过环境变量 BAOSTOCK_USER / BAOSTOCK_PASS 传入；
/// 任一缺失即 Skip，绝不在源码中硬编码任何回退凭据。
/// 通过 <c>[Collection("Live")]</c> 确保与其他 Live 测试串行执行，但使用独立 client。
/// </summary>
[Collection("Live")]
[Trait("Category", "Live")]
[Trait("RequiresCredentials", "true")]
public class AuthenticatedLoginTests
{
    [SkippableFact]
    public async Task Authenticated_Login_Logout_Roundtrip_Succeeds()
    {
        var user = Environment.GetEnvironmentVariable("BAOSTOCK_USER");
        var pass = Environment.GetEnvironmentVariable("BAOSTOCK_PASS");
        Skip.If(string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass),
            "BAOSTOCK_USER / BAOSTOCK_PASS 未设置，跳过认证用户 live 测试。");

        await using var client = new BaostockClient();

        var login = await client.LoginAsync(user!, pass!);
        Assert.Equal("0", login.ErrorCode);
        Assert.True(client.Session.IsLoggedIn);
        Assert.Equal(user, client.Session.UserId);

        await client.LogoutAsync();
        Assert.False(client.Session.IsLoggedIn);
    }
}
