using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Baostock.NET.Client;

namespace Baostock.NET.TestUI.Endpoints;

/// <summary>压测请求体（前端 POST /api/loadtest/run 提交）。</summary>
public sealed record LoadTestRequest
{
    /// <summary>目标 endpoint 路径（必须命中 EndpointRegistry 已注册路径）。</summary>
    public string TargetPath { get; init; } = string.Empty;

    /// <summary>透传给目标 endpoint 的 body（任意 JSON）。</summary>
    public JsonElement TargetBody { get; init; }

    /// <summary>"duration" | "count"，大小写不敏感。</summary>
    public string Mode { get; init; } = "duration";

    /// <summary>持续时长（秒）。Mode=duration 时使用，最大 300。</summary>
    public int? DurationSeconds { get; init; }

    /// <summary>总请求数。Mode=count 时使用，最大 100000。</summary>
    public int? TotalRequests { get; init; }

    /// <summary>并发 worker 数，最大 100。</summary>
    public int Concurrency { get; init; } = 10;

    /// <summary>暖机请求数（不计入统计），单线程顺序跑。</summary>
    public int WarmupRequests { get; init; } = 5;
}

/// <summary>延迟分位（毫秒）。</summary>
public sealed record LatencyStats(
    double Min,
    double P50,
    double P90,
    double P95,
    double P99,
    double Max,
    double Mean);

/// <summary>错误聚合项。</summary>
public sealed record ErrorBreakdownItem(string ErrorType, int Count);

/// <summary>压测响应。</summary>
public sealed record LoadTestResult(
    bool Ok,
    object Config,
    int TotalRequests,
    int SuccessCount,
    int ErrorCount,
    double ErrorRate,
    double Qps,
    long ElapsedMs,
    LatencyStats LatencyMs,
    IReadOnlyList<ErrorBreakdownItem> ErrorBreakdown,
    int WarmupSkipped,
    DateTime StartedAt,
    DateTime EndedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Error = null);

/// <summary>
/// 后端纯进程内压测执行器：直接调用 endpoint handler delegate（不走 HttpClient/Kestrel 自调），
/// 避免连接放大和单进程压力假象。同一时刻只允许 1 个任务运行。
/// </summary>
internal static class LoadTestRunner
{
    public const int MaxConcurrency = 100;
    public const int MaxDurationSeconds = 300;
    public const int MaxTotalRequests = 100_000;

    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>非阻塞抢占执行槽位。返回 false 表示已有任务在跑。</summary>
    public static bool TryAcquire() => Gate.Wait(0);

    public static void Release() => Gate.Release();

    public static async Task<LoadTestResult> RunAsync(
        LoadTestRequest req,
        RoutedEndpoint endpoint,
        BaostockClient client,
        CancellationToken ct)
    {
        var startedAt = DateTime.UtcNow;
        var latencies = new ConcurrentBag<double>();
        var errorTypes = new ConcurrentDictionary<string, int>();
        var success = 0;
        var errors = 0;

        var durationMode = string.Equals(req.Mode, "duration", StringComparison.OrdinalIgnoreCase);
        var durationMs = durationMode ? (long)(req.DurationSeconds ?? 30) * 1000L : 0L;
        var remaining = !durationMode ? Math.Max(0, req.TotalRequests ?? 0) : 0;
        var warmup = Math.Max(0, req.WarmupRequests);

        // ── warmup（单线程，错误吞掉，不计统计）──
        for (var i = 0; i < warmup; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                _ = await endpoint.Handler(req.TargetBody, client, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // ignored: 暖机阶段错误不参与统计
            }
        }

        var sw = Stopwatch.StartNew();

        async Task WorkerAsync()
        {
            while (!ct.IsCancellationRequested)
            {
                if (durationMode)
                {
                    if (sw.ElapsedMilliseconds >= durationMs) break;
                }
                else
                {
                    if (Interlocked.Decrement(ref remaining) < 0) break;
                }

                var ts0 = Stopwatch.GetTimestamp();
                try
                {
                    _ = await endpoint.Handler(req.TargetBody, client, ct).ConfigureAwait(false);
                    Interlocked.Increment(ref success);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref errors);
                    errorTypes.AddOrUpdate(ex.GetType().Name, 1, static (_, c) => c + 1);
                }
                finally
                {
                    var ts1 = Stopwatch.GetTimestamp();
                    var ms = (ts1 - ts0) * 1000.0 / Stopwatch.Frequency;
                    latencies.Add(ms);
                }
            }
        }

        var workers = new Task[req.Concurrency];
        for (var i = 0; i < req.Concurrency; i++)
        {
            workers[i] = Task.Run(WorkerAsync, ct);
        }

        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 取消时仍然把已收集的样本算出来。
        }

        sw.Stop();
        var endedAt = DateTime.UtcNow;

        var arr = latencies.ToArray();
        Array.Sort(arr);
        var stats = ComputeStats(arr);

        var total = success + errors;
        var elapsed = sw.ElapsedMilliseconds;
        var qps = elapsed > 0 ? Math.Round(total * 1000.0 / elapsed, 2) : 0;
        var errorRate = total > 0 ? Math.Round((double)errors / total, 4) : 0;

        var top5 = errorTypes
            .OrderByDescending(static kv => kv.Value)
            .Take(5)
            .Select(kv => new ErrorBreakdownItem(kv.Key, kv.Value))
            .ToList();

        var configEcho = new
        {
            targetPath = req.TargetPath,
            targetBody = req.TargetBody.ValueKind == JsonValueKind.Undefined ? null : (object?)req.TargetBody,
            mode = durationMode ? "duration" : "count",
            durationSeconds = req.DurationSeconds,
            totalRequests = req.TotalRequests,
            concurrency = req.Concurrency,
            warmupRequests = warmup,
        };

        return new LoadTestResult(
            Ok: true,
            Config: configEcho,
            TotalRequests: total,
            SuccessCount: success,
            ErrorCount: errors,
            ErrorRate: errorRate,
            Qps: qps,
            ElapsedMs: elapsed,
            LatencyMs: stats,
            ErrorBreakdown: top5,
            WarmupSkipped: warmup,
            StartedAt: startedAt,
            EndedAt: endedAt);
    }

    /// <summary>nearest-rank 分位：sorted[(int)Math.Ceiling(q*n) - 1]。</summary>
    private static LatencyStats ComputeStats(double[] sorted)
    {
        if (sorted.Length == 0)
        {
            return new LatencyStats(0, 0, 0, 0, 0, 0, 0);
        }

        double Pct(double q)
        {
            var idx = (int)Math.Ceiling(q * sorted.Length) - 1;
            if (idx < 0) idx = 0;
            if (idx >= sorted.Length) idx = sorted.Length - 1;
            return Math.Round(sorted[idx], 2);
        }

        double sum = 0;
        for (var i = 0; i < sorted.Length; i++)
        {
            sum += sorted[i];
        }

        return new LatencyStats(
            Min: Math.Round(sorted[0], 2),
            P50: Pct(0.50),
            P90: Pct(0.90),
            P95: Pct(0.95),
            P99: Pct(0.99),
            Max: Math.Round(sorted[^1], 2),
            Mean: Math.Round(sum / sorted.Length, 2));
    }
}
