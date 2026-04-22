using System.IO;
using System.Text;
using Baostock.NET.Protocol;

namespace Baostock.NET.Tests.Protocol;

public class FrameCodecTests
{
    /// <summary>
    /// 反向定位 tests/Fixtures：测试运行目录通常是 tests/Baostock.NET.Tests/bin/Debug/net9.0/，
    /// 上跳 4 级抵达 tests/。
    /// </summary>
    private static string FixturePath(params string[] segments)
    {
        var parts = new List<string> { AppContext.BaseDirectory, "..", "..", "..", "..", "Fixtures" };
        parts.AddRange(segments);
        return Path.GetFullPath(Path.Combine(parts.ToArray()));
    }

    private static byte[] ReadFixture(string name)
    {
        var path = FixturePath(name, "request.bin");
        Assert.True(File.Exists(path), $"fixture not found: {path}");
        var bytes = File.ReadAllBytes(path);
        // 上游帧落盘时尾部带 \n；DecodeFrame 约定不含 \n，去掉。
        if (bytes.Length > 0 && bytes[^1] == (byte)'\n')
        {
            return bytes[..^1];
        }
        return bytes;
    }

    [Fact]
    public void Crc32_KnownVector_Matches()
    {
        // 经典 CRC-32/IEEE 测试向量：crc32("123456789") == 0xCBF43926
        Assert.Equal(0xCBF43926u, FrameCodec.Crc32(Encoding.UTF8.GetBytes("123456789")));
    }

    [Fact]
    public void DecodeFrame_LoginRequest_Fixture()
    {
        var frame = ReadFixture("login");
        var (header, body) = FrameCodec.DecodeFrame(frame);

        Assert.Equal("00.9.10", header.Version);
        Assert.Equal(MessageTypes.LoginRequest, header.MessageType);
        Assert.Equal("login\u0001anonymous\u0001123456\u00010", body);
        Assert.Equal(body.Length, header.BodyLength);
    }

    [Fact]
    public void DecodeFrame_QueryHistoryKDataPlusRequest_Fixture()
    {
        var frame = ReadFixture("query_history_k_data_plus");
        var (header, _) = FrameCodec.DecodeFrame(frame);
        Assert.Equal(MessageTypes.GetKDataPlusRequest, header.MessageType);
    }

    [Fact]
    public void DecodeFrame_QueryStockBasicRequest_Fixture()
    {
        var frame = ReadFixture("query_stock_basic");
        var (header, _) = FrameCodec.DecodeFrame(frame);
        Assert.Equal(MessageTypes.QueryStockBasicRequest, header.MessageType);
    }

    [Fact]
    public void EncodeFrame_LoginBody_Roundtrip_Restores_Body()
    {
        var body = "login\u0001anonymous\u0001123456\u00010";
        var encoded = FrameCodec.EncodeFrame(MessageTypes.LoginRequest, body);

        var (header, decodedBody) = FrameCodec.DecodeFrame(encoded);
        Assert.Equal(MessageTypes.LoginRequest, header.MessageType);
        Assert.Equal(body, decodedBody);
        Assert.Equal(Encoding.UTF8.GetByteCount(body), header.BodyLength);
    }

    [Fact]
    public void EncodeFrame_LoginBody_ByteForByte_Matches_Fixture()
    {
        var body = "login\u0001anonymous\u0001123456\u00010";
        var encoded = FrameCodec.EncodeFrame(MessageTypes.LoginRequest, body);

        var fixture = ReadFixture("login");
        Assert.Equal(fixture, encoded);
    }

    [Fact]
    public void DecodeFrame_TamperedBody_FailsCrcCheck()
    {
        var frame = ReadFixture("login");
        var tampered = frame.ToArray();
        // 翻转 body 区中某个字节
        tampered[Framing.MessageHeaderLength + 5] ^= 0x20;
        Assert.Throws<InvalidDataException>(() => FrameCodec.DecodeFrame(tampered));
    }

    [Fact]
    public void Decompress_Roundtrip_Works()
    {
        var src = Encoding.UTF8.GetBytes("hello baostock zlib");
        using var ms = new MemoryStream();
        using (var z = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
        {
            z.Write(src, 0, src.Length);
        }
        var compressed = ms.ToArray();

        var roundtrip = FrameCodec.Decompress(compressed);
        Assert.Equal(src, roundtrip);
    }
}
