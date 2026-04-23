using Baostock.NET.Protocol;
using Baostock.NET.Tests.Client;

namespace Baostock.NET.Tests.Protocol;

public class ResponseParserTests
{
    [Fact]
    public void ParsePage_FromDecompressedFixture_ParsesCorrectly()
    {
        // Load fixture and decompress
        var raw = FixtureLoader.Load("query_history_k_data_plus", "response.bin");
        var stripped = FixtureLoader.StripTrailingNewline(raw);
        var header = MessageHeader.Parse(stripped.AsSpan(0, Framing.MessageHeaderLength));
        var compressed = stripped.AsSpan(Framing.MessageHeaderLength, header.BodyLength);
        var decompressed = FrameCodec.Decompress(compressed);
        var bodyText = System.Text.Encoding.UTF8.GetString(decompressed);

        var page = ResponseParser.ParsePage(bodyText);

        Assert.Equal("0", page.ErrorCode);
        Assert.Equal("success", page.ErrorMessage);
        Assert.Equal("query_history_k_data_plus", page.Method);
        Assert.Equal("anonymous", page.UserId);
        Assert.Equal(1, page.CurPageNum);
        Assert.Equal(10000, page.PerPageCount);
        Assert.Equal(14, page.Fields.Length);
        Assert.Equal("date", page.Fields[0]);
        Assert.Equal("isST", page.Fields[^1]);
        Assert.Equal(22, page.Rows.Count);
        Assert.Equal(14, page.Rows[0].Length);
        // 首行日期
        Assert.Equal("2024-01-02", page.Rows[0][0]);
        Assert.Equal("sh.600000", page.Rows[0][1]);
    }

    [Fact]
    public void ParsePage_ErrorResponse_ParsesMinimalFields()
    {
        var body = "10004020\x01错误的消息类型";
        var page = ResponseParser.ParsePage(body);

        Assert.Equal("10004020", page.ErrorCode);
        Assert.Equal("错误的消息类型", page.ErrorMessage);
        Assert.Empty(page.Rows);
    }

    [Fact]
    public void ParsePage_RowsLessThanPerPage_TotalPageEqualsCurrentPage()
    {
        var raw = FixtureLoader.Load("query_history_k_data_plus", "response.bin");
        var stripped = FixtureLoader.StripTrailingNewline(raw);
        var header = MessageHeader.Parse(stripped.AsSpan(0, Framing.MessageHeaderLength));
        var compressed = stripped.AsSpan(Framing.MessageHeaderLength, header.BodyLength);
        var decompressed = FrameCodec.Decompress(compressed);
        var bodyText = System.Text.Encoding.UTF8.GetString(decompressed);

        var page = ResponseParser.ParsePage(bodyText);

        // 22 rows < 10000 per page → total = current = 1
        Assert.Equal(1, page.TotalPageNum);
    }
}
