namespace Baostock.NET.Client;

/// <summary>
/// 客户端会话状态。由 <see cref="BaostockClient"/> 持有，登录后被填充。
/// </summary>
public sealed class BaostockSession
{
    /// <summary>登录使用的用户名（<c>anonymous</c> 表示匿名）。</summary>
    public string? UserId { get; internal set; }

    /// <summary>调用 <c>SetApiKey</c> 设置的 API Key；未设置时发送字符串 <c>"0"</c>。</summary>
    public string? ApiKey { get; set; }

    /// <summary>是否已成功登录。</summary>
    public bool IsLoggedIn { get; internal set; }
}
