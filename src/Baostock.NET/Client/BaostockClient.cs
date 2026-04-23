using System.Globalization;
using Baostock.NET.Protocol;

namespace Baostock.NET.Client;

/// <summary>
/// 登录响应解析结果。
/// </summary>
/// <param name="ErrorCode">服务端返回的错误码（成功时为 <c>"0"</c>）。</param>
/// <param name="ErrorMessage">服务端返回的错误信息（成功时通常为 <c>"success"</c>）。</param>
/// <param name="Method">服务端回填的方法名（通常为 <c>"login"</c>）。</param>
/// <param name="UserId">服务端回填的会话 user_id（多为时间戳串）。</param>
public sealed record LoginResult(string ErrorCode, string ErrorMessage, string? Method, string? UserId);

/// <summary>
/// Baostock 客户端入口。负责会话生命周期（登录/登出）与帧编解码协调。
/// 真正的字节级 I/O 委托给 <see cref="ITransport"/>，便于注入 fake 测试。
/// </summary>
public sealed partial class BaostockClient : IAsyncDisposable
{
    private readonly ITransport _transport;
    private readonly string? _autoUserId;
    private readonly string? _autoPassword;
    private LoginResult? _cachedLoginResult;

    /// <summary>当前会话状态。</summary>
    public BaostockSession Session { get; } = new();

    /// <summary>
    /// 是否启用"按需自动登录"。默认 <c>false</c>，避免隐式行为；
    /// 启用时配合构造函数注入的 <c>userId</c>/<c>password</c>，可由后续 query_* 在未登录时自动调用 <see cref="LoginAsync"/>。
    /// </summary>
    public bool AutoLogin { get; init; }

    /// <summary>
    /// 使用自定义传输层（默认 <see cref="TcpTransport"/>）构造，可选缓存 user/password 给 <see cref="AutoLogin"/> 使用。
    /// </summary>
    public BaostockClient(ITransport? transport = null, string? userId = null, string? password = null)
    {
        _transport = transport ?? new TcpTransport();
        _autoUserId = userId;
        _autoPassword = password;
    }

    /// <summary>
    /// 一站式：构造 + 登录。失败时自动 Dispose 并抛出。生产代码推荐入口。
    /// </summary>
    public static Task<BaostockClient> CreateAndLoginAsync(
        string userId = "anonymous",
        string password = "123456",
        CancellationToken ct = default)
        => CreateAndLoginAsync(transport: null, userId, password, ct);

    /// <summary>
    /// 测试/高级场景：注入自定义 transport 后登录。失败时自动 Dispose 并抛出。
    /// </summary>
    public static async Task<BaostockClient> CreateAndLoginAsync(
        ITransport? transport,
        string userId = "anonymous",
        string password = "123456",
        CancellationToken ct = default)
    {
        var client = new BaostockClient(transport, userId, password);
        try
        {
            await client.LoginAsync(userId, password, ct).ConfigureAwait(false);
            return client;
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>登录。匿名用 <c>anonymous / 123456</c>。已登录时直接返回缓存结果，不再发包。</summary>
    public async Task<LoginResult> LoginAsync(
        string userId = "anonymous",
        string password = "123456",
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentNullException.ThrowIfNull(password);

        // 已登录直接复用缓存（幂等保护，避免重复发包）。
        if (Session.IsLoggedIn && _cachedLoginResult is not null)
        {
            return _cachedLoginResult;
        }

        if (!_transport.IsConnected)
        {
            await _transport.ConnectAsync(ct).ConfigureAwait(false);
        }

        var apiKey = string.IsNullOrEmpty(Session.ApiKey) ? "0" : Session.ApiKey;
        var body = string.Concat(
            "login", Framing.MessageSplit,
            userId, Framing.MessageSplit,
            password, Framing.MessageSplit,
            apiKey);

        var frame = FrameCodec.EncodeFrame(MessageTypes.LoginRequest, body);
        await _transport.SendAsync(frame, ct).ConfigureAwait(false);

        var responseFrame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
        var (header, respBody) = FrameCodec.DecodeFrame(responseFrame);

        if (header.MessageType == MessageTypes.Exception)
        {
            ThrowFromExceptionFrame(respBody);
        }

        // body 形如 error_code\x01error_msg\x01method\x01user_id
        var parts = respBody.Split(Framing.MessageSplit[0]);
        var errorCode = parts.Length > 0 ? parts[0] : string.Empty;
        var errorMsg = parts.Length > 1 ? parts[1] : string.Empty;
        var method = parts.Length > 2 ? parts[2] : null;
        var serverUserId = parts.Length > 3 ? parts[3] : null;

        if (!string.Equals(errorCode, "0", StringComparison.Ordinal))
        {
            throw new BaostockException(errorCode, errorMsg);
        }

        Session.UserId = userId;
        Session.IsLoggedIn = true;
        _cachedLoginResult = new LoginResult(errorCode, errorMsg, method, serverUserId);
        return _cachedLoginResult;
    }

    /// <summary>登出当前会话。未登录时抛 <see cref="InvalidOperationException"/>。</summary>
    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (!Session.IsLoggedIn)
        {
            throw new InvalidOperationException("not logged in");
        }

        if (!_transport.IsConnected)
        {
            await _transport.ConnectAsync(ct).ConfigureAwait(false);
        }

        var userId = Session.UserId ?? "anonymous";
        var ts = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var body = string.Concat(
            "logout", Framing.MessageSplit,
            userId, Framing.MessageSplit,
            ts);

        var frame = FrameCodec.EncodeFrame(MessageTypes.LogoutRequest, body);
        await _transport.SendAsync(frame, ct).ConfigureAwait(false);

        var responseFrame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
        var (header, respBody) = FrameCodec.DecodeFrame(responseFrame);

        if (header.MessageType == MessageTypes.Exception)
        {
            ThrowFromExceptionFrame(respBody);
        }

        var parts = respBody.Split(Framing.MessageSplit[0]);
        var errorCode = parts.Length > 0 ? parts[0] : string.Empty;
        var errorMsg = parts.Length > 1 ? parts[1] : string.Empty;
        if (!string.Equals(errorCode, "0", StringComparison.Ordinal))
        {
            throw new BaostockException(errorCode, errorMsg);
        }

        Session.IsLoggedIn = false;
        Session.UserId = null;
        _cachedLoginResult = null;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _transport.DisposeAsync();

    /// <summary>
    /// 在未登录时按 <see cref="AutoLogin"/> + 构造时缓存的 user/password 自动登录；
    /// 已登录则 no-op。供未来 query_* 实现复用，外部代码无需直接调用。
    /// </summary>
    internal async Task EnsureLoggedInAsync(CancellationToken ct = default)
    {
        if (Session.IsLoggedIn)
        {
            return;
        }
        if (!AutoLogin || string.IsNullOrEmpty(_autoUserId) || _autoPassword is null)
        {
            throw new InvalidOperationException(
                "not logged in (and AutoLogin disabled or credentials missing)");
        }
        await LoginAsync(_autoUserId, _autoPassword, ct).ConfigureAwait(false);
    }

    private static void ThrowFromExceptionFrame(string body)
    {
        var parts = body.Split(Framing.MessageSplit[0]);
        var code = parts.Length > 0 ? parts[0] : "unknown";
        var msg = parts.Length > 1 ? parts[1] : "服务端返回了错误帧 (MSG=04) 但未携带错误信息。";
        throw new BaostockException(code, msg);
    }
}
