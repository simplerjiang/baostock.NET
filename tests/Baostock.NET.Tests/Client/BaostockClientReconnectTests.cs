using Baostock.NET.Client;
using Baostock.NET.Protocol;

namespace Baostock.NET.Tests.Client;

/// <summary>
/// B1 (v1.2.0-preview5) 自愈流程单元测试。全 mock 不打实网。
/// </summary>
public class BaostockClientReconnectTests
{
    private static byte[] LoadLoginSuccessFrame()
        => FixtureLoader.StripTrailingNewline(FixtureLoader.Load("login", "response.bin"));

    private static byte[] LoadFixtureFrame(string fixtureName)
        => FixtureLoader.StripTrailingNewline(FixtureLoader.Load(fixtureName, "response.bin"));

    [Fact]
    public async Task Query_AfterTransportBroken_AutoReconnectsAndRelogins()
    {
        // 帧队列：① 首次 login  ② reconnect 后的 relogin  ③ 重连后的 trade-dates 查询响应
        var transport = new FakeTransport();
        transport.EnqueueResponse(LoadLoginSuccessFrame());
        transport.EnqueueResponse(LoadLoginSuccessFrame());
        transport.EnqueueResponse(LoadFixtureFrame("query_trade_dates"));

        await using var client = new BaostockClient(transport);
        await client.LoginAsync(); // 填充 _credentials，Session.IsLoggedIn=true

        Assert.True(client.IsLoggedIn);
        Assert.Equal(1, transport.ConnectCalls);

        // 模拟 socket 半死（IsConnected → false）
        transport.MarkBroken();
        Assert.False(client.IsLoggedIn,
            "B1：IsLoggedIn 语义已改为 Session.IsLoggedIn && transport.IsConnected");

        // 触发底层 query：EnsureLoggedInAsync 必须走 reconnect+relogin 分支
        var rows = new List<Baostock.NET.Models.TradeDateRow>();
        await foreach (var row in client.QueryTradeDatesAsync(
            startDate: "2024-01-01", endDate: "2024-01-15"))
        {
            rows.Add(row);
        }

        Assert.True(rows.Count > 0);
        Assert.Equal(2, transport.ConnectCalls); // 初始 1 + reconnect 1
        // 发送顺序：login → relogin → trade-dates query（至少 3 帧，query 可能分页多帧）
        Assert.True(transport.SentFrames.Count >= 3);
        Assert.True(client.IsLoggedIn);
    }

    [Fact]
    public async Task Query_WhenReloginConnectFails_MapsToBaostockException()
    {
        // 用一个"首次 ConnectAsync 正常、MarkBrokenAndFailNextConnect 后再 Connect 抛"的 mock。
        var transport = new ReconnectFailingFakeTransport();
        transport.EnqueueResponse(LoadLoginSuccessFrame());

        await using var client = new BaostockClient(transport);
        await client.LoginAsync();

        transport.MarkBrokenAndFailNextConnect();

        var ex = await Assert.ThrowsAsync<BaostockException>(async () =>
        {
            await foreach (var _ in client.QueryTradeDatesAsync(
                startDate: "2024-01-01", endDate: "2024-01-15"))
            {
            }
        });

        Assert.Equal("reconnect_failed", ex.ErrorCode);
    }

    [Fact]
    public async Task Query_WithoutLoginAndWithoutAutoLogin_DoesNotAutoLogin()
    {
        // 没登录过 → _credentials=null；AutoLogin=false → 不会自动 login。
        // 当前语义是抛 InvalidOperationException（保留原有行为，B1 不改这条路径）。
        var transport = new FakeTransport();
        await using var client = new BaostockClient(transport);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in client.QueryTradeDatesAsync(
                startDate: "2024-01-01", endDate: "2024-01-15"))
            {
            }
        });

        Assert.Equal(0, transport.ConnectCalls);
        Assert.Empty(transport.SentFrames);
    }
}

/// <summary>
/// 用于"relogin 时 ConnectAsync 失败"的 mock：首次 ConnectAsync 正常（供 login 用），
/// 在 <see cref="MarkBrokenAndFailNextConnect"/> 之后下一次 ConnectAsync 抛 IOException。
/// </summary>
internal sealed class ReconnectFailingFakeTransport : ITransport
{
    private readonly Queue<byte[]> _responses = new();
    private bool _connected;
    private bool _failNextConnect;

    public List<byte[]> SentFrames { get; } = new();
    public int ConnectCalls { get; private set; }
    public bool IsConnected => _connected;

    public void EnqueueResponse(byte[] frame) => _responses.Enqueue(frame);

    public void MarkBrokenAndFailNextConnect()
    {
        _connected = false;
        _failNextConnect = true;
    }

    public ValueTask ConnectAsync(CancellationToken ct = default)
    {
        ConnectCalls++;
        if (_failNextConnect)
        {
            _failNextConnect = false;
            _connected = false;
            throw new IOException("simulated reconnect failure");
        }
        _connected = true;
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
            throw new InvalidOperationException("no queued response");
        }
        return ValueTask.FromResult(_responses.Dequeue());
    }

    public ValueTask DisposeAsync()
    {
        _connected = false;
        return ValueTask.CompletedTask;
    }
}
