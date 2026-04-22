namespace Baostock.NET.Client;

/// <summary>
/// 服务端业务错误（包含 MSG=04 错误帧 与 error_code != "0" 的常规错误响应）。
/// </summary>
public sealed class BaostockException : Exception
{
    /// <summary>Baostock 错误码字符串（如 <c>10004020</c>）。</summary>
    public string ErrorCode { get; }

    /// <summary>构造一个业务错误。</summary>
    public BaostockException(string code, string message)
        : base(message)
    {
        ErrorCode = code;
    }
}
