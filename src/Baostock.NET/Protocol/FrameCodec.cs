using System.Globalization;
using System.IO.Compression;
using System.IO.Hashing;
using System.Text;

namespace Baostock.NET.Protocol;

/// <summary>
/// 帧编解码、CRC32 与 zlib 解压。纯函数层，不引用 socket，可单测。
/// 帧布局（不含末尾物理分隔符 <see cref="Framing.Delimiter"/>）：
/// <code>
/// [Header(21)] [Body(BodyLength)] \x01 [CRC32(decimal ascii)]
/// </code>
/// CRC32 在 <c>Header || Body</c> 上计算（与上游 Python 客户端 <c>zlib.crc32(head_body)</c> 完全一致），
/// 然后用十进制字符串拼接（**不做零填充**，因实测 fixture 验证）。
/// </summary>
public static class FrameCodec
{
    private static readonly byte MessageSplitByte = (byte)Framing.MessageSplit[0];

    /// <summary>构造一个完整帧（不含末尾 <see cref="Framing.Delimiter"/>）。</summary>
    public static byte[] EncodeFrame(string messageType, string body)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(body);

        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var header = new MessageHeader(BaostockServer.ClientVersion, messageType, bodyBytes.Length);
        var headerBytes = header.Encode();

        Span<byte> headerAndBody = new byte[headerBytes.Length + bodyBytes.Length];
        headerBytes.CopyTo(headerAndBody);
        bodyBytes.CopyTo(headerAndBody[headerBytes.Length..]);

        var crc = Crc32(headerAndBody);
        var crcStr = crc.ToString(CultureInfo.InvariantCulture);
        var crcBytes = Encoding.ASCII.GetBytes(crcStr);

        var frame = new byte[headerAndBody.Length + 1 + crcBytes.Length];
        headerAndBody.CopyTo(frame);
        frame[headerAndBody.Length] = MessageSplitByte;
        crcBytes.CopyTo(frame.AsSpan(headerAndBody.Length + 1));
        return frame;
    }

    /// <summary>
    /// 从一个完整帧（不含末尾 <see cref="Framing.Delimiter"/>）拆出 header 与 body，并校验 CRC32。
    /// <para>
    /// <b>注意</b>：body/crc 的边界<b>不</b>使用 header.BodyLength 字段判定，而是从帧末尾向前找最后一个
    /// <c>\x01</c>。原因：服务端在某些响应（如 authenticated login 的中文错误消息）里，BodyLength 字段
    /// 按字符数而非字节数填充，与 UTF-8 字节长度不一致；但 CRC 始终是纯 ASCII 十进制数字（不含 <c>\x01</c>），
    /// 故"最后一个 <c>\x01</c>"始终是 body 与 crc 的真实边界。
    /// 该方法仅适用于<b>非压缩</b>响应；压缩响应（MSG=96）的 body 是二进制 zlib 流，可能包含 0x01 字节，
    /// 应按 header.BodyLength 自行切分（见 <see cref="TcpTransport.ReadFrameAsync"/>）。
    /// </para>
    /// </summary>
    public static (MessageHeader Header, string Body) DecodeFrame(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < Framing.MessageHeaderLength + 1)
        {
            throw new FormatException($"帧长度过短：{frame.Length} 字节。");
        }

        var header = MessageHeader.Parse(frame[..Framing.MessageHeaderLength]);

        // 从尾部向前找最后一个 \x01，将 body 与 crc 切开。
        var lastSplit = frame.LastIndexOf(MessageSplitByte);
        if (lastSplit < Framing.MessageHeaderLength)
        {
            throw new FormatException("帧 body 后缺少 \\x01 分隔符。");
        }

        var body = frame[Framing.MessageHeaderLength..lastSplit];

        var crcSpan = frame[(lastSplit + 1)..];
        if (crcSpan.IsEmpty)
        {
            throw new FormatException("帧缺少 CRC32 字段。");
        }

        var crcStr = Encoding.ASCII.GetString(crcSpan);
        if (!uint.TryParse(crcStr, NumberStyles.None, CultureInfo.InvariantCulture, out var expectedCrc))
        {
            throw new FormatException($"CRC32 不是合法的十进制无符号整数：'{crcStr}'。");
        }

        var actualCrc = Crc32(frame[..lastSplit]);
        if (actualCrc != expectedCrc)
        {
            throw new InvalidDataException(
                $"CRC32 校验失败：期望 {expectedCrc}，实际 {actualCrc}。");
        }

        return (header, Encoding.UTF8.GetString(body));
    }

    /// <summary>对 zlib 压缩的字节流解压（用于 96 GetKDataPlusResponse）。</summary>
    public static byte[] Decompress(ReadOnlySpan<byte> compressed)
    {
        using var input = new MemoryStream(compressed.ToArray(), writable: false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>计算 CRC32（IEEE 多项式，与 Python <c>zlib.crc32</c> 一致）。</summary>
    public static uint Crc32(ReadOnlySpan<byte> data)
        => System.IO.Hashing.Crc32.HashToUInt32(data);
}
