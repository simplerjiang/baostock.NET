using System.Text;

namespace Baostock.NET.Protocol;

/// <summary>
/// 21 字节定长消息头：<c>Version(7) + \x01 + MessageType(2) + \x01 + BodyLength(10位左补零)</c>，UTF-8 编码。
/// 对应 Python <c>data/messageheader.to_message_header</c>。注意 BodyLength 是消息体字节长度，不含头部。
/// </summary>
/// <param name="Version">协议版本号，如 <c>00.9.10</c>。</param>
/// <param name="MessageType">消息类型，2 位 ASCII，如 <c>00</c> 表示登录请求。</param>
/// <param name="BodyLength">消息体字节长度。</param>
public readonly record struct MessageHeader(string Version, string MessageType, int BodyLength)
{
    /// <summary>从 21 字节切片解析出消息头。</summary>
    public static MessageHeader Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Framing.MessageHeaderLength)
        {
            throw new ArgumentException(
                $"消息头长度必须是 {Framing.MessageHeaderLength} 字节，实际 {bytes.Length}。",
                nameof(bytes));
        }

        var text = Encoding.UTF8.GetString(bytes);
        var split = Framing.MessageSplit[0];
        var parts = text.Split(split);
        if (parts.Length != 3)
        {
            throw new FormatException($"消息头格式错误，期望 3 段以 \\x01 分隔，实际：'{text}'。");
        }

        var version = parts[0];
        var msgType = parts[1];
        var lenStr = parts[2];

        if (msgType.Length != 2)
        {
            throw new FormatException($"消息头 MessageType 必须 2 位，实际：'{msgType}'。");
        }

        if (lenStr.Length != Framing.MessageBodyLengthDigits)
        {
            throw new FormatException(
                $"消息头 BodyLength 必须 {Framing.MessageBodyLengthDigits} 位，实际：'{lenStr}'。");
        }

        if (!int.TryParse(lenStr, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var bodyLen))
        {
            throw new FormatException($"消息头 BodyLength 不是非负整数：'{lenStr}'。");
        }

        return new MessageHeader(version, msgType, bodyLen);
    }

    /// <summary>编码为 21 字节 UTF-8。</summary>
    public byte[] Encode()
    {
        if (string.IsNullOrEmpty(Version))
        {
            throw new InvalidOperationException("Version 不能为空。");
        }

        if (MessageType is null || MessageType.Length != 2)
        {
            throw new InvalidOperationException("MessageType 必须 2 个 ASCII 字符。");
        }

        if (BodyLength < 0)
        {
            throw new InvalidOperationException("BodyLength 不能为负。");
        }

        var lenStr = BodyLength.ToString(
            "D" + Framing.MessageBodyLengthDigits.ToString(System.Globalization.CultureInfo.InvariantCulture),
            System.Globalization.CultureInfo.InvariantCulture);
        if (lenStr.Length != Framing.MessageBodyLengthDigits)
        {
            throw new InvalidOperationException(
                $"BodyLength 编码后超过 {Framing.MessageBodyLengthDigits} 位：{BodyLength}。");
        }

        var text = Version + Framing.MessageSplit + MessageType + Framing.MessageSplit + lenStr;
        var bytes = Encoding.UTF8.GetBytes(text);
        if (bytes.Length != Framing.MessageHeaderLength)
        {
            throw new InvalidOperationException(
                $"消息头编码长度 {bytes.Length} 与预期 {Framing.MessageHeaderLength} 不一致。检查 Version 长度。");
        }

        return bytes;
    }
}
