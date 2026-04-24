using Baostock.NET.Http;
using Baostock.NET.Models;

namespace Baostock.NET.Tests.Realtime;

internal sealed class FakeRealtimeSource : IDataSource<string[], IReadOnlyList<RealtimeQuote>>
{
    private readonly TimeSpan _delay;
    private readonly Func<string[], IReadOnlyList<RealtimeQuote>>? _result;
    private readonly Exception? _error;
    public int CallCount;
    public CancellationToken LastToken;

    public FakeRealtimeSource(string name, int priority, TimeSpan delay,
        Func<string[], IReadOnlyList<RealtimeQuote>>? result = null,
        Exception? error = null)
    {
        Name = name;
        Priority = priority;
        _delay = delay;
        _result = result;
        _error = error;
    }

    public string Name { get; }
    public int Priority { get; }

    public async Task<IReadOnlyList<RealtimeQuote>> FetchAsync(string[] request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref CallCount);
        LastToken = cancellationToken;
        if (_delay > TimeSpan.Zero)
        {
            await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
        }
        if (_error != null)
        {
            throw _error;
        }
        return _result!(request);
    }
}

public class MultiSourceClientTests
{
    private static RealtimeQuote MakeQuote(string code, string source, decimal last) =>
        new(code, "TEST", 0, 0, last, 0, 0, null, null, 0, 0,
            new DateTime(2026, 4, 24, 10, 0, 0, DateTimeKind.Unspecified), source);

    [Fact]
    public async Task Sina_Fail_FallbackToTencent()
    {
        var sina = new FakeRealtimeSource("Sina", 0, TimeSpan.Zero,
            error: new DataSourceException("Sina", "EMPTY"));
        var tencent = new FakeRealtimeSource("Tencent", 1, TimeSpan.FromMilliseconds(20),
            result: codes => codes.Select(c => MakeQuote(c, "Tencent", 100m)).ToList());
        var em = new FakeRealtimeSource("EastMoney", 2, TimeSpan.FromMilliseconds(100),
            result: codes => codes.Select(c => MakeQuote(c, "EastMoney", 99m)).ToList());

        var runner = new HedgedRequestRunner<string[], IReadOnlyList<RealtimeQuote>>(
            new IDataSource<string[], IReadOnlyList<RealtimeQuote>>[] { sina, tencent, em },
            "realtime", TimeSpan.FromMilliseconds(500), new SourceHealthRegistry());

        var result = await runner.ExecuteAsync(new[] { "SH600519" }, default);

        Assert.Equal("Tencent", result.SourceName);
        Assert.Equal("Tencent", result.Value[0].Source);
        Assert.Equal(1, sina.CallCount);
        Assert.Equal(1, tencent.CallCount);
    }

    [Fact]
    public async Task HedgeWinner_CancelsLosers()
    {
        // Sina 慢（800ms）；hedge 100ms 后启动 Tencent（20ms 完成）→ Tencent 胜出 → Sina 应被取消。
        var sinaCanceled = false;
        var sina = new FakeRealtimeSource("Sina", 0, TimeSpan.FromMilliseconds(800),
            result: _ => Array.Empty<RealtimeQuote>());
        var tencent = new FakeRealtimeSource("Tencent", 1, TimeSpan.FromMilliseconds(20),
            result: codes => codes.Select(c => MakeQuote(c, "Tencent", 100m)).ToList());

        var runner = new HedgedRequestRunner<string[], IReadOnlyList<RealtimeQuote>>(
            new IDataSource<string[], IReadOnlyList<RealtimeQuote>>[] { sina, tencent },
            "realtime", TimeSpan.FromMilliseconds(100), new SourceHealthRegistry());

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await runner.ExecuteAsync(new[] { "SH600519" }, default);
        sw.Stop();

        Assert.Equal("Tencent", result.SourceName);
        // Tencent 胜出后，Sina 的 token 应已被请求取消
        await Task.Delay(50);
        sinaCanceled = sina.LastToken.IsCancellationRequested;
        Assert.True(sinaCanceled, "Loser source token should be canceled.");
        // 应远小于 800ms
        Assert.True(sw.ElapsedMilliseconds < 600, $"Hedge should win quickly, took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task AllSourcesFail_ThrowsAggregate()
    {
        var sina = new FakeRealtimeSource("Sina", 0, TimeSpan.Zero,
            error: new DataSourceException("Sina", "EMPTY"));
        var tencent = new FakeRealtimeSource("Tencent", 1, TimeSpan.Zero,
            error: new DataSourceException("Tencent", "EMPTY"));
        var em = new FakeRealtimeSource("EastMoney", 2, TimeSpan.Zero,
            error: new DataSourceException("EastMoney", "EMPTY"));

        var runner = new HedgedRequestRunner<string[], IReadOnlyList<RealtimeQuote>>(
            new IDataSource<string[], IReadOnlyList<RealtimeQuote>>[] { sina, tencent, em },
            "realtime", TimeSpan.FromMilliseconds(500), new SourceHealthRegistry());

        var ex = await Assert.ThrowsAsync<AllSourcesFailedException>(
            () => runner.ExecuteAsync(new[] { "SH600519" }, default));
        Assert.Equal("realtime", ex.DataKind);
        Assert.Equal(3, ex.InnerExceptions.Count);
    }

    [Fact]
    public async Task PartialBatchFailure_PropagatesAsException()
    {
        // 模拟某源返回行数 != 入参数 → 视为部分失败抛 DataSourceException → hedge fallback。
        var sina = new FakeRealtimeSource("Sina", 0, TimeSpan.FromMilliseconds(10),
            error: new DataSourceException("Sina", "Sina returned 1 quotes, expected 2."));
        var tencent = new FakeRealtimeSource("Tencent", 1, TimeSpan.FromMilliseconds(10),
            result: codes => codes.Select(c => MakeQuote(c, "Tencent", 100m)).ToList());

        var runner = new HedgedRequestRunner<string[], IReadOnlyList<RealtimeQuote>>(
            new IDataSource<string[], IReadOnlyList<RealtimeQuote>>[] { sina, tencent },
            "realtime", TimeSpan.FromMilliseconds(500), new SourceHealthRegistry());

        var result = await runner.ExecuteAsync(new[] { "SH600519", "SZ000001" }, default);
        Assert.Equal("Tencent", result.SourceName);
        Assert.Equal(2, result.Value.Count);
    }
}
