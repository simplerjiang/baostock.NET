using System.Net.Sockets;
using System.Net;
using System.Text;
using Baostock.NET.Http;

namespace Baostock.NET.Tests.Http;

/// <summary>
/// 验证 <see cref="HttpDataClient"/> 的 timeout 是否真正覆盖 header + body 全过程。
/// 借助本地 TcpListener 充当慢响应 HTTP 服务器。
/// </summary>
public class HttpDataClientTimeoutTests
{
    [Fact]
    public async Task GetStringAsync_BodyDelayLongerThanTimeout_ThrowsTimeout()
    {
        // 服务端：快速发送 status line + header（含 Content-Length: 100），然后挂 5 秒不发 body。
        await using var server = new SlowHttpServer(async (stream, ct) =>
        {
            await DrainRequestAsync(stream, ct).ConfigureAwait(false);

            var headers = "HTTP/1.1 200 OK\r\n" +
                          "Content-Type: text/plain\r\n" +
                          "Content-Length: 100\r\n" +
                          "Connection: close\r\n" +
                          "\r\n";
            var hb = Encoding.ASCII.GetBytes(headers);
            await stream.WriteAsync(hb, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);

            // 故意挂起 5 秒后才发 body —— 期望在此期间客户端 timeout 已触发
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 服务器关闭时忽略
            }
        });

        var client = HttpDataClient.CreateForTesting();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await client.GetStringAsync(
                $"http://127.0.0.1:{server.Port}/",
                timeout: TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        });

        sw.Stop();
        // timeout 应该在 1s 附近触发，给充裕余量上限 4s
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(4),
            $"GetStringAsync 在 body 慢读取时未按 timeout 取消，实际耗时 {sw.Elapsed}。");
    }

    [Fact]
    public async Task PostFormAsync_HeaderDelayLongerThanTimeout_ThrowsTimeout()
    {
        // 服务端：连请求都不读，accept 后直接挂 5 秒
        await using var server = new SlowHttpServer(async (stream, ct) =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        });

        var client = HttpDataClient.CreateForTesting();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await client.PostFormAsync(
                $"http://127.0.0.1:{server.Port}/",
                new Dictionary<string, string> { ["k"] = "v" },
                timeout: TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        });

        sw.Stop();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(4),
            $"PostFormAsync 在 header 阻塞时未按 timeout 取消，实际耗时 {sw.Elapsed}。");
    }

    private static async Task DrainRequestAsync(NetworkStream stream, CancellationToken ct)
    {
        // 简化：只读到 \r\n\r\n 即可（不处理 body，因为是 GET）
        var buf = new byte[4096];
        var sb = new StringBuilder();
        while (!ct.IsCancellationRequested)
        {
            var n = await stream.ReadAsync(buf, ct).ConfigureAwait(false);
            if (n == 0)
            {
                return;
            }
            sb.Append(Encoding.ASCII.GetString(buf, 0, n));
            if (sb.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                return;
            }
        }
    }

    /// <summary>
    /// 极简 HTTP 慢响应服务器：每个连接交给 <c>handler</c> 自定义读写。
    /// </summary>
    private sealed class SlowHttpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;

        public int Port { get; }

        public SlowHttpServer(Func<NetworkStream, CancellationToken, Task> handler)
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

            _loop = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    TcpClient? client = null;
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
                                await handler(stream, _cts.Token).ConfigureAwait(false);
                            }
                        }
                        catch
                        {
                            // 测试场景，异常静默吞掉
                        }
                    });
                }
            });
        }

        public async ValueTask DisposeAsync()
        {
            try { _cts.Cancel(); } catch { }
            try { _listener.Stop(); } catch { }
            try { await _loop.ConfigureAwait(false); } catch { }
            _cts.Dispose();
        }
    }
}
