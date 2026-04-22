namespace Baostock.NET.Protocol;

/// <summary>
/// Baostock 协议传输层抽象。负责字节级 I/O，不解析 body，不做 CRC/zlib 处理。
/// </summary>
public interface ITransport : IAsyncDisposable
{
    /// <summary>建立到上游服务端的连接。已连接时为幂等。</summary>
    ValueTask ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// 把一条已编码好的完整 frame（<see cref="FrameCodec.EncodeFrame(string,string)"/> 的输出）
    /// 写入连接，自动追加 <see cref="Framing.Delimiter"/>。
    /// </summary>
    ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default);

    /// <summary>
    /// 按头部 BodyLength 字段精确读够一帧，返回去掉物理终止符（<c>\n</c> 或 <c>&lt;![CDATA[]]&gt;\n</c>）后的完整 frame，
    /// 即 <c>header || body || \x01 || crc</c>，可直接交给 <see cref="FrameCodec.DecodeFrame(System.ReadOnlySpan{byte})"/>。
    /// </summary>
    ValueTask<byte[]> ReceiveFrameAsync(CancellationToken ct = default);

    /// <summary>当前连接是否处于已连接状态。</summary>
    bool IsConnected { get; }
}
