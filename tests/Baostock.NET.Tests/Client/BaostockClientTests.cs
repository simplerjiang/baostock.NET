using System.Text;
using Baostock.NET.Client;
using Baostock.NET.Protocol;

namespace Baostock.NET.Tests.Client;

/// <summary>
/// 仅用于单元测试：捕获已发送的 frames，按预设队列回放 ReceiveFrameAsync。
/// </summary>
internal sealed class FakeTransport : ITransport
{
    private readonly Queue<byte[]> _responses = new();
    public List<byte[]> SentFrames { get; } = new();
    public bool IsConnected { get; private set; }
    public int ConnectCalls { get; private set; }

    public void EnqueueResponse(byte[] frameWithoutNewline) => _responses.Enqueue(frameWithoutNewline);

    public ValueTask ConnectAsync(CancellationToken ct = default)
    {
        ConnectCalls++;
        IsConnected = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
    {
        SentFrames.Add(frame.ToArray());
        return ValueTask.CompletedTask;
    }

    public ValueTask<byte[]> ReceiveFrameAsync(CancellationToken ct = default)
    {
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("FakeTransport 没有预填响应。");
        }
        return ValueTask.FromResult(_responses.Dequeue());
    }

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}

internal static class FixtureLoader
{
    /// <summary>
    /// 从仓库根 tests/Fixtures/&lt;name&gt;/&lt;file&gt; 读取黄金样本字节。
    /// 测试运行目录是 tests/Baostock.NET.Tests/bin/Debug/net9.0/，向上找 4 层到仓库根。
    /// </summary>
    public static byte[] Load(string captureName, string fileName)
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "tests", "Fixtures", captureName, fileName);
            if (File.Exists(candidate))
            {
                return File.ReadAllBytes(candidate);
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException($"找不到 fixture：tests/Fixtures/{captureName}/{fileName}");
    }

    /// <summary>
    /// 剥掉 fixture 文件末尾的物理终止符（可选 <c>&lt;![CDATA[]]&gt;</c> + 必有的 <c>\n</c>），
    /// 返回与 <see cref="ITransport.ReceiveFrameAsync"/> 契约一致的帧（仅 header||body||\x01||crc）。
    /// </summary>
    public static byte[] StripTrailingNewline(byte[] raw)
    {
        var end = raw.Length;
        if (end > 0 && raw[end - 1] == (byte)'\n')
        {
            end--;
        }
        var cdata = Encoding.ASCII.GetBytes("<![CDATA[]]>");
        if (end >= cdata.Length && raw.AsSpan(end - cdata.Length, cdata.Length).SequenceEqual(cdata))
        {
            end -= cdata.Length;
        }
        var trimmed = new byte[end];
        Array.Copy(raw, trimmed, end);
        return trimmed;
    }
}

public class BaostockClientTests
{
    [Fact]
    public async Task LoginAsync_WriesByteIdenticalRequestFrame_FromGoldenFixture()
    {
        var requestRaw = FixtureLoader.Load("login", "request.bin");
        var expectedFrameNoNewline = FixtureLoader.StripTrailingNewline(requestRaw);
        var responseFrame = FixtureLoader.StripTrailingNewline(
            FixtureLoader.Load("login", "response.bin"));

        var transport = new FakeTransport();
        transport.EnqueueResponse(responseFrame);
        await using var client = new BaostockClient(transport);

        var result = await client.LoginAsync();

        Assert.Equal(1, transport.ConnectCalls);
        Assert.Single(transport.SentFrames);
        Assert.Equal(expectedFrameNoNewline, transport.SentFrames[0]);
        Assert.Equal("0", result.ErrorCode);
        Assert.Equal("success", result.ErrorMessage);
        Assert.Equal("login", result.Method);
        Assert.True(client.Session.IsLoggedIn);
        Assert.Equal("anonymous", client.Session.UserId);
    }

    [Fact]
    public async Task LoginAsync_OnExceptionFrame_ThrowsBaostockException()
    {
        var errorFrame = BuildExceptionFrame("10004020", "错误的消息类型");
        var transport = new FakeTransport();
        transport.EnqueueResponse(errorFrame);
        await using var client = new BaostockClient(transport);

        var ex = await Assert.ThrowsAsync<BaostockException>(() => client.LoginAsync());
        Assert.Equal("10004020", ex.ErrorCode);
        Assert.Equal("错误的消息类型", ex.Message);
        Assert.False(client.Session.IsLoggedIn);
    }

    [Fact]
    public async Task LoginAsync_OnNonZeroErrorCode_ThrowsBaostockException()
    {
        // 构造一个常规 LoginResponse(MSG=01) 但 error_code != "0"
        var body = string.Concat(
            "10001001", Framing.MessageSplit,
            "user not exist", Framing.MessageSplit,
            "login", Framing.MessageSplit,
            "anonymous");
        var frame = FrameCodec.EncodeFrame(MessageTypes.LoginResponse, body);

        var transport = new FakeTransport();
        transport.EnqueueResponse(frame);
        await using var client = new BaostockClient(transport);

        var ex = await Assert.ThrowsAsync<BaostockException>(() => client.LoginAsync("baduser"));
        Assert.Equal("10001001", ex.ErrorCode);
        Assert.False(client.Session.IsLoggedIn);
    }

    [Fact]
    public async Task LogoutAsync_OnSuccess_ClearsSession()
    {
        var body = string.Concat(
            "0", Framing.MessageSplit,
            "success", Framing.MessageSplit,
            "logout", Framing.MessageSplit,
            "anonymous");
        var responseFrame = FrameCodec.EncodeFrame(MessageTypes.LogoutResponse, body);

        var transport = new FakeTransport();
        // 先登录拿到会话状态，再 logout
        transport.EnqueueResponse(LoadLoginSuccessFrame());
        transport.EnqueueResponse(responseFrame);
        await using var client = new BaostockClient(transport);
        await client.LoginAsync();

        await client.LogoutAsync();

        Assert.Equal(2, transport.SentFrames.Count);
        // 校验第二条发出的是 LogoutRequest(MSG=02) 帧，body 起头应为 "logout\x01anonymous\x01"
        var (header, sentBody) = FrameCodec.DecodeFrame(transport.SentFrames[1]);
        Assert.Equal(MessageTypes.LogoutRequest, header.MessageType);
        Assert.StartsWith("logout" + Framing.MessageSplit + "anonymous" + Framing.MessageSplit, sentBody, StringComparison.Ordinal);
        Assert.False(client.Session.IsLoggedIn);
        Assert.Null(client.Session.UserId);
    }

    [Fact]
    public async Task LogoutAsync_OnExceptionFrame_ThrowsBaostockException()
    {
        var errorFrame = BuildExceptionFrame("10004020", "错误的消息类型");
        var transport = new FakeTransport();
        transport.EnqueueResponse(LoadLoginSuccessFrame());
        transport.EnqueueResponse(errorFrame);
        await using var client = new BaostockClient(transport);
        await client.LoginAsync();

        var ex = await Assert.ThrowsAsync<BaostockException>(() => client.LogoutAsync());
        Assert.Equal("10004020", ex.ErrorCode);
        // logout 失败时不强制清状态（保留信息便于排查）
        Assert.True(client.Session.IsLoggedIn);
    }

    private static byte[] BuildExceptionFrame(string code, string message)
    {
        var body = code + Framing.MessageSplit + message;
        return FrameCodec.EncodeFrame(MessageTypes.Exception, body);
    }

    private static byte[] LoadLoginSuccessFrame()
        => FixtureLoader.StripTrailingNewline(FixtureLoader.Load("login", "response.bin"));
}
