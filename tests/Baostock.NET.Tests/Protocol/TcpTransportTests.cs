using System.Net.Sockets;
using Baostock.NET.Protocol;
using Baostock.NET.Tests.Client; // 复用 FixtureLoader

namespace Baostock.NET.Tests.Protocol;

/// <summary>
/// 仅用于单元测试：包装一段字节，每次 ReadAsync 只返回 1 字节，强制 ReadFrameAsync 多次读才能拼出整帧。
/// 用来覆盖 TCP 任意切片下的帧边界正确性。
/// </summary>
internal sealed class ChunkedStream : Stream
{
    private readonly byte[] _data;
    private int _pos;
    private readonly int _maxChunk;

    public ChunkedStream(byte[] data, int maxChunk = 1)
    {
        _data = data;
        _maxChunk = Math.Max(1, maxChunk);
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _data.Length;
    public override long Position { get => _pos; set => throw new NotSupportedException(); }

    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => ReadCore(buffer.AsSpan(offset, count));

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ReadCore(buffer.Span));
    }

    private int ReadCore(Span<byte> dest)
    {
        if (_pos >= _data.Length || dest.Length == 0)
        {
            return 0;
        }
        var n = Math.Min(Math.Min(_maxChunk, dest.Length), _data.Length - _pos);
        _data.AsSpan(_pos, n).CopyTo(dest);
        _pos += n;
        return n;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>
/// 永远不返回数据的流。仅用于 cancellation 测试，ReadAsync 一直挂起直到 ct 触发。
/// </summary>
internal sealed class BlockingStream : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)).ConfigureAwait(false))
        {
            return await tcs.Task.ConfigureAwait(false);
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

public class TcpTransportTests
{
    /// <summary>
    /// 任意切片：把 login response.bin（保留末尾 \n）按 1 字节/次喂给 ReadFrameAsync，
    /// 断言拼回的帧字节级 == 去掉物理终止符后的 fixture（即 FixtureLoader.StripTrailingNewline 的输出）。
    /// </summary>
    [Fact]
    public async Task ReadFrameAsync_LoginResponse_AssemblesByteIdentical_UnderOneBytePerRead()
    {
        var raw = FixtureLoader.Load("login", "response.bin");
        var expected = FixtureLoader.StripTrailingNewline(raw);

        await using var stream = new ChunkedStream(raw, maxChunk: 1);
        var actual = await TcpTransport.ReadFrameAsync(stream);

        Assert.Equal(expected, actual);
        // 校验流被完整消费（trailing 的 \n 也必须被吃掉，未来才能接收下一帧）
        var extra = new byte[1];
        Assert.Equal(0, await stream.ReadAsync(extra));
    }

    /// <summary>
    /// 任意切片 + CDATA 压缩结尾：MSG=96 K 线响应。ReadFrameAsync 必须按 BodyLength 权威读 body，
    /// 然后吃掉 \x01<crc>\n + "<![CDATA[]]>\n"，保证流游标位于下一帧起点；
    /// 拿出 body 后能用 FrameCodec.Decompress 解压成功。
    /// </summary>
    [Fact]
    public async Task ReadFrameAsync_KDataPlusResponse_ConsumesCDataTrailer_AndBodyDecompresses()
    {
        var raw = FixtureLoader.Load("query_history_k_data_plus", "response.bin");
        await using var stream = new ChunkedStream(raw, maxChunk: 3);

        var frame = await TcpTransport.ReadFrameAsync(stream);

        // 流应被完整消费（CDATA + 末尾 \n 都被吃掉了）
        var extra = new byte[1];
        Assert.Equal(0, await stream.ReadAsync(extra));

        // 头解析正确，msg=96
        var header = MessageHeader.Parse(frame.AsSpan(0, Framing.MessageHeaderLength));
        Assert.Equal(MessageTypes.GetKDataPlusResponse, header.MessageType);
        Assert.True(header.BodyLength > 0);

        // body 是 zlib 流，能解压出非空内容
        var body = frame.AsSpan(Framing.MessageHeaderLength, header.BodyLength).ToArray();
        var decompressed = FrameCodec.Decompress(body);
        Assert.True(decompressed.Length > 0);
    }

    /// <summary>
    /// 连接到本机一个铁定关闭的端口（127.0.0.1:1）应抛 SocketException，状态保持 IsConnected=false。
    /// </summary>
    [Fact]
    public async Task ConnectAsync_DeadPort_ThrowsSocketException_AndKeepsIsConnectedFalse()
    {
        await using var transport = new TcpTransport("127.0.0.1", 1);
        Assert.False(transport.IsConnected);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<SocketException>(async () => await transport.ConnectAsync(cts.Token));

        Assert.False(transport.IsConnected);
    }

    /// <summary>
    /// 流读到一半被关：构造一个被截断的 fixture（只保留 header+body 的一部分），
    /// ReadFrameAsync 应抛 EndOfStreamException。
    /// </summary>
    [Fact]
    public async Task ReadFrameAsync_StreamClosedMidBody_ThrowsEndOfStream()
    {
        var raw = FixtureLoader.Load("login", "response.bin");
        // 截断到 header 之后只剩 5 字节 body，body 一定不够
        var truncated = new byte[Framing.MessageHeaderLength + 5];
        Array.Copy(raw, truncated, truncated.Length);

        await using var stream = new ChunkedStream(truncated, maxChunk: 1);
        await Assert.ThrowsAsync<EndOfStreamException>(async () => await TcpTransport.ReadFrameAsync(stream));
    }

    /// <summary>
    /// trailing 段读到一半连接关闭：完整 header+body 已到，但 trailing 没读到 \n 就 EOF。
    /// </summary>
    [Fact]
    public async Task ReadFrameAsync_StreamClosedMidTrailing_ThrowsEndOfStream()
    {
        var raw = FixtureLoader.Load("login", "response.bin");
        // 去掉末尾 \n（保留 \x01+crc 部分），让 trailing 永远等不到 \n 直至 EOF
        Assert.Equal((byte)'\n', raw[^1]);
        var truncated = new byte[raw.Length - 1];
        Array.Copy(raw, truncated, truncated.Length);

        await using var stream = new ChunkedStream(truncated, maxChunk: 1);
        await Assert.ThrowsAsync<EndOfStreamException>(async () => await TcpTransport.ReadFrameAsync(stream));
    }

    /// <summary>
    /// 在 ReadFrameAsync 中触发 CancellationToken，应抛 OperationCanceledException。
    /// </summary>
    [Fact]
    public async Task ReadFrameAsync_Cancellation_ThrowsOperationCanceled()
    {
        await using var stream = new BlockingStream();
        using var cts = new CancellationTokenSource();

        var task = TcpTransport.ReadFrameAsync(stream, cts.Token);
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
    }
}
