using System.Text;
using Baostock.NET.Protocol;

namespace Baostock.NET.Tests.Protocol;

public class MessageHeaderTests
{
    [Fact]
    public void Encode_Then_Parse_Roundtrip()
    {
        var header = new MessageHeader(BaostockServer.ClientVersion, MessageTypes.LoginRequest, 24);
        var bytes = header.Encode();
        Assert.Equal(Framing.MessageHeaderLength, bytes.Length);

        var parsed = MessageHeader.Parse(bytes);
        Assert.Equal(header, parsed);
    }

    [Fact]
    public void Encode_Login_Header_Matches_Expected_Wire_Bytes()
    {
        var header = new MessageHeader("00.9.10", "00", 24);
        var bytes = header.Encode();
        var expected = Encoding.UTF8.GetBytes("00.9.10\u000100\u00010000000024");
        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Encode_BodyLength_Zero_Pads_To_Ten_Digits()
    {
        var header = new MessageHeader("00.9.10", "01", 0);
        var text = Encoding.UTF8.GetString(header.Encode());
        Assert.EndsWith("\u00010000000000", text);
    }

    [Fact]
    public void Encode_BodyLength_MaxTenDigits_Works()
    {
        var header = new MessageHeader("00.9.10", "95", int.MaxValue);
        var text = Encoding.UTF8.GetString(header.Encode());
        Assert.EndsWith("\u0001" + int.MaxValue.ToString("D10"), text);
    }

    [Fact]
    public void Parse_With_NonNumeric_BodyLength_Throws()
    {
        var bad = Encoding.UTF8.GetBytes("00.9.10\u000100\u0001ABCDEFGHIJ");
        Assert.Throws<FormatException>(() => MessageHeader.Parse(bad));
    }

    [Fact]
    public void Parse_With_Wrong_Length_Throws()
    {
        Assert.Throws<ArgumentException>(() => MessageHeader.Parse(new byte[20]));
        Assert.Throws<ArgumentException>(() => MessageHeader.Parse(new byte[22]));
    }

    [Fact]
    public void Encode_Negative_BodyLength_Throws()
    {
        var header = new MessageHeader("00.9.10", "00", -1);
        Assert.Throws<InvalidOperationException>(() => header.Encode());
    }
}
