using System.Net.Sockets;
using System.Text;

namespace Baostock.NET.Protocol;

/// <summary>
/// 基于 <see cref="TcpClient"/> 的默认 <see cref="ITransport"/> 实现。单连接长会话。
/// 帧读取核心 <see cref="ReadFrameAsync(System.IO.Stream, System.Threading.CancellationToken)"/>
/// 是 stream 无关的纯静态方法，便于在不依赖 socket 的前提下做单测（任意切片、CDATA 结尾、EOS、cancel）。
/// </summary>
public sealed class TcpTransport : ITransport
{
    // 服务端在响应帧末尾追加的"消息间分隔符"，见 reference/baostock-python/util/socketutil.py。
    private static readonly byte[] CDataSuffix = Encoding.ASCII.GetBytes("<![CDATA[]]>");
    private const byte NewLineByte = (byte)'\n';

    private readonly string _host;
    private readonly int _port;
    private TcpClient? _client;
    private Stream? _stream;
    private bool _connected;
    private bool _disposed;

    // B1 (v1.2.0-preview5)：socket I/O 失败后置 true，使 IsConnected 立刻反映"半死"状态，
    // 触发上层 BaostockClient.EnsureConnectedAsync 走重连+relogin 路径。
    private bool _isBroken;

    /// <summary>用默认 <see cref="BaostockServer.Host"/> / <see cref="BaostockServer.Port"/> 构造。</summary>
    public TcpTransport()
        : this(BaostockServer.Host, BaostockServer.Port)
    {
    }

    /// <summary>用自定义 host/port 构造。</summary>
    public TcpTransport(string host, int port)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);
        _host = host;
        _port = port;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 健康检查使用标准 TCP "half-dead" 探测：<c>Socket.Poll(0, SelectRead) == true &amp;&amp; Available == 0</c>
    /// 表示对端已关闭（FIN 已收到，但本地还没正式 close）。Poll(0, ...) 是 O(1) 非阻塞调用，
    /// 不会显著拖慢正常请求路径。同时一旦 SendAsync/ReceiveFrameAsync 抛过 IO/SocketException，
    /// <c>_isBroken</c> 锁定为 true，无需再 Poll。
    /// </remarks>
    public bool IsConnected
    {
        get
        {
            if (_disposed || _isBroken || !_connected || _stream is null)
            {
                return false;
            }
            var socket = _client?.Client;
            if (socket is null || !socket.Connected)
            {
                return false;
            }
            try
            {
                // Poll 返回 true 且 Available==0 → 对端已关闭半开连接。
                if (socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0)
                {
                    return false;
                }
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask ConnectAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsConnected)
        {
            return;
        }

        // B1：之前的连接可能因 socket 故障被标 broken，先彻底拆除残留 _client/_stream
        // 再建新连接到原 host:port。Dispose 是幂等的，重复进入安全。
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        _connected = false;
        _isBroken = false;

        var client = new TcpClient { NoDelay = true };
        try
        {
            await client.ConnectAsync(_host, _port, ct).ConfigureAwait(false);
        }
        catch
        {
            client.Dispose();
            throw;
        }
        _client = client;
        _stream = client.GetStream();
        _connected = true;
    }

    /// <inheritdoc />
    public async ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var stream = _stream ?? throw new InvalidOperationException("尚未 ConnectAsync。");

        try
        {
            await stream.WriteAsync(frame, ct).ConfigureAwait(false);
            // 物理消息分隔符
            var nl = new byte[] { NewLineByte };
            await stream.WriteAsync(nl, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is System.IO.IOException or SocketException)
        {
            _isBroken = true;
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask<byte[]> ReceiveFrameAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var stream = _stream ?? throw new InvalidOperationException("尚未 ConnectAsync。");
        try
        {
            return await ReadFrameAsync(stream, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is System.IO.IOException or SocketException or EndOfStreamException)
        {
            _isBroken = true;
            throw;
        }
    }

    /// <summary>
    /// 从任意 <see cref="Stream"/> 读完整一帧。
    /// <para>
    /// <b>压缩消息</b>（MSG=96 K 线响应）：body 是 zlib 二进制流，可能包含 <c>0x01</c>，必须按 header.BodyLength
    /// 权威读取；trailer 形如 <c>\x01&lt;crc&gt;\n&lt;![CDATA[]]&gt;\n</c>。
    /// </para>
    /// <para>
    /// <b>非压缩消息</b>（MSG=01/03 等）：服务端 header.BodyLength 在含中文的响应里按字符数计而非字节数计，
    /// 不可靠；故按 <c>&lt;![CDATA[]]&gt;\n</c> 物理结尾标记一路读到底，再剥掉该 13 字节标记。
    /// </para>
    /// 返回的字节序列为 <c>header || body || \x01 || crc</c>，可直接喂给 <see cref="FrameCodec.DecodeFrame"/>。
    /// 暴露为 public static 是为了：1) 在测试中注入任意切片的 fake stream；2) 给高级使用方复用。
    /// </summary>
    public static async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // 1) 读 21 字节头
        var headerBuf = new byte[Framing.MessageHeaderLength];
        await ReadExactlyAsync(stream, headerBuf, ct).ConfigureAwait(false);
        var header = MessageHeader.Parse(headerBuf);

        if (IsCompressedMessageType(header.MessageType))
        {
            return await ReadCompressedFrameBodyAsync(stream, headerBuf, header, ct).ConfigureAwait(false);
        }

        // 非压缩：流式读到 "<![CDATA[]]>\n" 结束标记，剥掉标记，前面就是 body||\x01||crc
        return await ReadNonCompressedFrameBodyAsync(stream, headerBuf, ct).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadCompressedFrameBodyAsync(
        Stream stream, byte[] headerBuf, MessageHeader header, CancellationToken ct)
    {
        // body 长度权威
        var bodyBuf = new byte[header.BodyLength];
        if (header.BodyLength > 0)
        {
            await ReadExactlyAsync(stream, bodyBuf, ct).ConfigureAwait(false);
        }

        // \x01 + crc(变长 ASCII 数字) 直到 \n
        using var trailing = new MemoryStream();
        var oneByte = new byte[1];
        while (true)
        {
            var n = await stream.ReadAsync(oneByte.AsMemory(0, 1), ct).ConfigureAwait(false);
            if (n == 0)
            {
                throw new EndOfStreamException("读 trailing 时连接已关闭。");
            }
            if (oneByte[0] == NewLineByte)
            {
                break;
            }
            trailing.WriteByte(oneByte[0]);
        }
        var trailingBytes = trailing.ToArray();

        // 紧接着的 13 字节必须是 "<![CDATA[]]>\n"
        var cdataBuf = new byte[CDataSuffix.Length + 1];
        await ReadExactlyAsync(stream, cdataBuf, ct).ConfigureAwait(false);
        if (!cdataBuf.AsSpan(0, CDataSuffix.Length).SequenceEqual(CDataSuffix)
            || cdataBuf[^1] != NewLineByte)
        {
            throw new FormatException(
                "压缩消息（MSG=96）trailer 末尾不是预期的 \"<![CDATA[]]>\\n\"。");
        }

        var frame = new byte[headerBuf.Length + bodyBuf.Length + trailingBytes.Length];
        headerBuf.CopyTo(frame, 0);
        bodyBuf.CopyTo(frame, headerBuf.Length);
        trailingBytes.CopyTo(frame, headerBuf.Length + bodyBuf.Length);
        return frame;
    }

    private static async Task<byte[]> ReadNonCompressedFrameBodyAsync(
        Stream stream, byte[] headerBuf, CancellationToken ct)
    {
        // 滚动缓冲：每次读 1 字节追加，直到末尾 13 字节匹配 "<![CDATA[]]>\n" 即停。
        var endMarker = new byte[CDataSuffix.Length + 1];
        CDataSuffix.CopyTo(endMarker, 0);
        endMarker[^1] = NewLineByte;

        using var buf = new MemoryStream();
        var oneByte = new byte[1];
        while (true)
        {
            var n = await stream.ReadAsync(oneByte.AsMemory(0, 1), ct).ConfigureAwait(false);
            if (n == 0)
            {
                throw new EndOfStreamException("读非压缩 frame 时连接已关闭，未见 \"<![CDATA[]]>\\n\" 结束标记。");
            }
            buf.WriteByte(oneByte[0]);
            if (buf.Length >= endMarker.Length)
            {
                var bufBytes = buf.GetBuffer();
                var endStart = (int)(buf.Length - endMarker.Length);
                if (bufBytes.AsSpan(endStart, endMarker.Length).SequenceEqual(endMarker))
                {
                    var bodyAndTrailer = new byte[endStart];
                    Array.Copy(bufBytes, bodyAndTrailer, endStart);
                    var frame = new byte[headerBuf.Length + bodyAndTrailer.Length];
                    headerBuf.CopyTo(frame, 0);
                    bodyAndTrailer.CopyTo(frame, headerBuf.Length);
                    return frame;
                }
            }
        }
    }

    private static bool IsCompressedMessageType(string messageType)
        => string.Equals(messageType, MessageTypes.GetKDataPlusResponse, StringComparison.Ordinal);

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }
        _disposed = true;
        _connected = false;
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        return ValueTask.CompletedTask;
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), ct).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException(
                    $"期望读 {buffer.Length} 字节，实际只读到 {offset}（连接被对端关闭）。");
            }
            offset += read;
        }
    }
}
