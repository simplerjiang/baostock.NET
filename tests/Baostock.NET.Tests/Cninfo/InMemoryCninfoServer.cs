using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Baostock.NET.Tests.Cninfo;

/// <summary>
/// 集成测试用的最小 HTTP 服务器，仿 <c>SlowHttpServer</c> 模式：
/// 用 <see cref="TcpListener"/> 监听 loopback 随机端口，手写 HTTP/1.1 响应头。
/// 路由通过 <see cref="Setup(string, string, byte[])"/> 注入，POST 返回注入的 body（当 JSON 用），
/// GET 返回注入的 binary payload，支持 <c>Range: bytes=N-</c> → 206 Partial Content。
/// </summary>
internal sealed class InMemoryCninfoServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private readonly ConcurrentDictionary<string, byte[]> _routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CapturedRequest> _received = new();
    private readonly object _receivedLock = new();

    public int Port { get; }

    public Uri BaseUri { get; }

    public InMemoryCninfoServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        BaseUri = new Uri($"http://127.0.0.1:{Port}");
        _loop = Task.Run(AcceptLoopAsync);
    }

    /// <summary>抓取到的所有请求快照（复制返回，线程安全）。</summary>
    public IReadOnlyList<CapturedRequest> Received
    {
        get
        {
            lock (_receivedLock)
            {
                return _received.ToArray();
            }
        }
    }

    /// <summary>为指定 method + path 注入响应体。</summary>
    public void Setup(string method, string path, byte[] payload)
    {
        _routes[Key(method, path)] = payload;
    }

    /// <summary>为指定 method + path 注入文本响应（UTF-8 编码）。</summary>
    public void Setup(string method, string path, string text)
    {
        Setup(method, path, Encoding.UTF8.GetBytes(text));
    }

    private static string Key(string method, string path)
        => method.ToUpperInvariant() + " " + path;

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient? client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            var captured = client;
            _ = Task.Run(async () =>
            {
                try
                {
                    using (captured)
                    {
                        using var stream = captured.GetStream();
                        await HandleConnectionAsync(stream, _cts.Token).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // 测试场景，异常静默
                }
            });
        }
    }

    private async Task HandleConnectionAsync(NetworkStream stream, CancellationToken ct)
    {
        var (requestLine, headers, body) = await ReadRequestAsync(stream, ct).ConfigureAwait(false);
        if (requestLine is null)
        {
            return;
        }

        var parts = requestLine.Split(' ');
        if (parts.Length < 2)
        {
            return;
        }
        var method = parts[0];
        var fullPath = parts[1];
        var queryIdx = fullPath.IndexOf('?');
        var path = queryIdx >= 0 ? fullPath.Substring(0, queryIdx) : fullPath;

        lock (_receivedLock)
        {
            _received.Add(new CapturedRequest(method, path, fullPath, headers, body));
        }

        if (!_routes.TryGetValue(Key(method, path), out var payload))
        {
            await WriteTextAsync(stream,
                "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
                ct).ConfigureAwait(false);
            return;
        }

        // Range 支持（仅 GET 生效）
        if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            && TryParseRangeStart(headers, out var rangeStart)
            && rangeStart >= 0
            && rangeStart < payload.LongLength)
        {
            var remaining = new byte[payload.Length - rangeStart];
            Array.Copy(payload, rangeStart, remaining, 0, remaining.Length);
            var header = "HTTP/1.1 206 Partial Content\r\n"
                + "Content-Type: application/pdf\r\n"
                + "Content-Length: " + remaining.Length.ToString(CultureInfo.InvariantCulture) + "\r\n"
                + "Content-Range: bytes " + rangeStart.ToString(CultureInfo.InvariantCulture)
                    + "-" + (payload.Length - 1).ToString(CultureInfo.InvariantCulture)
                    + "/" + payload.Length.ToString(CultureInfo.InvariantCulture) + "\r\n"
                + "Connection: close\r\n\r\n";
            await WriteTextAsync(stream, header, ct).ConfigureAwait(false);
            await stream.WriteAsync(remaining, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            return;
        }

        var contentType = string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf"
            : "application/json; charset=utf-8";
        var headerText = "HTTP/1.1 200 OK\r\n"
            + "Content-Type: " + contentType + "\r\n"
            + "Content-Length: " + payload.Length.ToString(CultureInfo.InvariantCulture) + "\r\n"
            + "Connection: close\r\n\r\n";
        await WriteTextAsync(stream, headerText, ct).ConfigureAwait(false);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task<(string? requestLine, Dictionary<string, string> headers, byte[] body)> ReadRequestAsync(
        NetworkStream stream, CancellationToken ct)
    {
        var buffer = new byte[8192];
        var acc = new List<byte>(8192);
        int headerEnd = -1;
        while (headerEnd < 0)
        {
            var n = await stream.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (n == 0)
            {
                return (null, new Dictionary<string, string>(), Array.Empty<byte>());
            }
            for (int i = 0; i < n; i++) acc.Add(buffer[i]);
            headerEnd = IndexOfDoubleCrLf(acc);
            if (acc.Count > 65536)
            {
                return (null, new Dictionary<string, string>(), Array.Empty<byte>());
            }
        }

        var accArr = acc.ToArray();
        var headerText = Encoding.ASCII.GetString(accArr, 0, headerEnd);
        var lines = headerText.Split("\r\n");
        var requestLine = lines.Length > 0 ? lines[0] : null;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }
            var name = line.Substring(0, colon).Trim();
            var value = line.Substring(colon + 1).Trim();
            headers[name] = value;
        }

        var bodyStart = headerEnd + 4;
        var alreadyReadBody = accArr.Length - bodyStart;

        int contentLength = 0;
        if (headers.TryGetValue("Content-Length", out var clStr)
            && int.TryParse(clStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cl))
        {
            contentLength = cl;
        }

        byte[] body;
        if (contentLength <= 0)
        {
            body = Array.Empty<byte>();
        }
        else
        {
            body = new byte[contentLength];
            var copyFromAcc = Math.Min(alreadyReadBody, contentLength);
            if (copyFromAcc > 0)
            {
                Array.Copy(accArr, bodyStart, body, 0, copyFromAcc);
            }
            var remaining = contentLength - copyFromAcc;
            var offset = copyFromAcc;
            while (remaining > 0)
            {
                var r = await stream.ReadAsync(body.AsMemory(offset, remaining), ct).ConfigureAwait(false);
                if (r == 0)
                {
                    break;
                }
                offset += r;
                remaining -= r;
            }
        }

        return (requestLine, headers, body);
    }

    private static int IndexOfDoubleCrLf(List<byte> buf)
    {
        for (int i = 0; i + 3 < buf.Count; i++)
        {
            if (buf[i] == (byte)'\r' && buf[i + 1] == (byte)'\n'
                && buf[i + 2] == (byte)'\r' && buf[i + 3] == (byte)'\n')
            {
                return i;
            }
        }
        return -1;
    }

    private static bool TryParseRangeStart(Dictionary<string, string> headers, out long start)
    {
        start = -1;
        if (!headers.TryGetValue("Range", out var value) || string.IsNullOrEmpty(value))
        {
            return false;
        }
        var eq = value.IndexOf('=');
        if (eq < 0)
        {
            return false;
        }
        var spec = value.Substring(eq + 1);
        var dash = spec.IndexOf('-');
        if (dash <= 0)
        {
            return false;
        }
        var startStr = spec.Substring(0, dash);
        return long.TryParse(startStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out start);
    }

    private static Task WriteTextAsync(NetworkStream stream, string text, CancellationToken ct)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        return stream.WriteAsync(bytes, 0, bytes.Length, ct);
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _listener.Stop(); } catch { }
        try { _loop.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts.Dispose();
    }
}

/// <summary>服务器抓取到的请求快照。</summary>
internal sealed record CapturedRequest(
    string Method,
    string Path,
    string FullPath,
    IReadOnlyDictionary<string, string> Headers,
    byte[] Body);
