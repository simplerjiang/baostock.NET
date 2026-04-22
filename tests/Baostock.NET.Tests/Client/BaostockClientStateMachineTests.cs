using Baostock.NET.Client;
using Baostock.NET.Protocol;

namespace Baostock.NET.Tests.Client;

public class BaostockClientStateMachineTests
{
    /// <summary>
    /// 未 Login 直接 LogoutAsync 应抛 InvalidOperationException("not logged in")。
    /// </summary>
    [Fact]
    public async Task LogoutAsync_WithoutLogin_ThrowsInvalidOperation()
    {
        var transport = new FakeTransport();
        await using var client = new BaostockClient(transport);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.LogoutAsync());
        Assert.Contains("not logged in", ex.Message, StringComparison.Ordinal);
        Assert.Empty(transport.SentFrames);
        Assert.Equal(0, transport.ConnectCalls);
    }

    /// <summary>
    /// 重复 LoginAsync：第二次直接返回缓存 LoginResult，不再发包。
    /// </summary>
    [Fact]
    public async Task LoginAsync_SecondCall_ReturnsCached_DoesNotResend()
    {
        var responseFrame = LoadLoginSuccessFrame();
        var transport = new FakeTransport();
        transport.EnqueueResponse(responseFrame);
        await using var client = new BaostockClient(transport);

        var first = await client.LoginAsync();
        var second = await client.LoginAsync();

        Assert.Same(first, second);
        Assert.Single(transport.SentFrames);
        Assert.Equal(1, transport.ConnectCalls);
        Assert.True(client.Session.IsLoggedIn);
    }

    /// <summary>
    /// CreateAndLoginAsync(fakeTransport)：成功路径，返回的 client 已登录。
    /// </summary>
    [Fact]
    public async Task CreateAndLoginAsync_WithFakeTransport_ReturnsLoggedInClient()
    {
        var responseFrame = LoadLoginSuccessFrame();
        var transport = new FakeTransport();
        transport.EnqueueResponse(responseFrame);

        await using var client = await BaostockClient.CreateAndLoginAsync(transport);

        Assert.True(client.Session.IsLoggedIn);
        Assert.Equal("anonymous", client.Session.UserId);
        Assert.Single(transport.SentFrames);
    }

    /// <summary>
    /// CreateAndLoginAsync 在登录失败时应自动 Dispose 并把异常向上传。
    /// </summary>
    [Fact]
    public async Task CreateAndLoginAsync_OnLoginFailure_DisposesAndRethrows()
    {
        var body = string.Concat(
            "10001001", Framing.MessageSplit,
            "user not exist", Framing.MessageSplit,
            "login", Framing.MessageSplit,
            "baduser");
        var errorFrame = FrameCodec.EncodeFrame(MessageTypes.LoginResponse, body);
        var transport = new FakeTransport();
        transport.EnqueueResponse(errorFrame);

        var ex = await Assert.ThrowsAsync<BaostockException>(
            () => BaostockClient.CreateAndLoginAsync(transport, "baduser", "x"));
        Assert.Equal("10001001", ex.ErrorCode);
        // 失败时 transport 应已被 Dispose
        Assert.False(transport.IsConnected);
    }

    private static byte[] LoadLoginSuccessFrame()
        => FixtureLoader.StripTrailingNewline(FixtureLoader.Load("login", "response.bin"));
}
