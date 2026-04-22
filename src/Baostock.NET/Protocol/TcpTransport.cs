using System.Net.Sockets;
using System.Text;

namespace Baostock.NET.Protocol;

/// <summary>
/// 基于 <see cref="TcpClient"/> 的默认 <see cref="ITransport"/> 实现。单连接长会话。
/// </summary>
public sealed class TcpTransport : ITransport
{
    // 服务端在响应帧末尾追加的"消息间分隔符"，见 reference/baostock-python/util/socketutil.py。
    private static readonly byte[] CDataSuffix = Encoding.ASCII.GetBytes("<![CDATA[]]>");
    private const byte NewLineByte = (byte)'\n';

    private readonly string _host;
    private readonly int _port;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private bool _disposed;

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
    public bool IsConnected => _client?.Connected == true && _stream is not null;

    /// <inheritdoc />
    public async ValueTask ConnectAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsConnected)
        {
            return;
        }

        _client = new TcpClient { NoDelay = true };
        await _client.ConnectAsync(_host, _port, ct).ConfigureAwait(false);
        _stream = _client.GetStream();
    }

    /// <inheritdoc />
    public async ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var stream = _stream ?? throw new InvalidOperationException("尚未 ConnectAsync。");

        await stream.WriteAsync(frame, ct).ConfigureAwait(false);
        // 物理消息分隔符
        var nl = new byte[] { NewLineByte };
        await stream.WriteAsync(nl, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<byte[]> ReceiveFrameAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var stream = _stream ?? throw new InvalidOperationException("尚未 ConnectAsync。");

        // 1) 读 21 字节头
        var headerBuf = new byte[Framing.MessageHeaderLength];
        await ReadExactlyAsync(stream, headerBuf, ct).ConfigureAwait(false);
        var header = MessageHeader.Parse(headerBuf);

        // 2) 读 body
        var bodyBuf = new byte[header.BodyLength];
        if (header.BodyLength > 0)
        {
            await ReadExactlyAsync(stream, bodyBuf, ct).ConfigureAwait(false);
        }

        // 3) 读 trailing：\x01 + crc(变长 ASCII 数字) + 可选 "<![CDATA[]]>" + \n
        //    简单稳妥：逐字节读直到遇到 \n，再剥掉可选的 CDATA 后缀。
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
        // 剥掉可能存在的 "<![CDATA[]]>" 后缀
        if (trailingBytes.Length >= CDataSuffix.Length)
        {
            var tailStart = trailingBytes.Length - CDataSuffix.Length;
            if (trailingBytes.AsSpan(tailStart).SequenceEqual(CDataSuffix))
            {
                Array.Resize(ref trailingBytes, tailStart);
            }
        }

        // 4) 拼回 header || body || trailing(=\x01 + crc)
        var frame = new byte[headerBuf.Length + bodyBuf.Length + trailingBytes.Length];
        headerBuf.CopyTo(frame, 0);
        bodyBuf.CopyTo(frame, headerBuf.Length);
        trailingBytes.CopyTo(frame, headerBuf.Length + bodyBuf.Length);
        return frame;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }
        _disposed = true;
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        return ValueTask.CompletedTask;
    }

    private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
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
