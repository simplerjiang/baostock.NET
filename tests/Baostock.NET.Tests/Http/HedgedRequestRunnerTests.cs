using Baostock.NET.Http;

namespace Baostock.NET.Tests.Http;

internal sealed class FakeSource : IDataSource<int, string>
{
    private readonly TimeSpan _delay;
    private readonly Func<int, string>? _result;
    private readonly Exception? _error;
    public int CallCount;
    public CancellationToken LastToken;

    public FakeSource(string name, int priority, TimeSpan delay, Func<int, string>? result = null, Exception? error = null)
    {
        Name = name;
        Priority = priority;
        _delay = delay;
        _result = result ?? (req => $"{name}:{req}");
        _error = error;
    }

    public string Name { get; }
    public int Priority { get; }

    public async Task<string> FetchAsync(int request, CancellationToken cancellationToken)
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

public class HedgedRequestRunnerTests
{
    [Fact]
    public async Task SingleSource_Success()
    {
        var src = new FakeSource("a", 0, TimeSpan.FromMilliseconds(10));
        var runner = new HedgedRequestRunner<int, string>(
            new[] { src }, "k", TimeSpan.FromMilliseconds(500), new SourceHealthRegistry());
        var r = await runner.ExecuteAsync(7, default);
        Assert.Equal("a:7", r.Value);
        Assert.Equal("a", r.SourceName);
        Assert.Equal(1, src.CallCount);
    }

    [Fact]
    public async Task PrimaryFastSuccess_BackupNotStarted()
    {
        var primary = new FakeSource("p", 0, TimeSpan.FromMilliseconds(50));
        var backup = new FakeSource("b", 1, TimeSpan.FromMilliseconds(10));
        var runner = new HedgedRequestRunner<int, string>(
            new IDataSource<int, string>[] { primary, backup },
            "k", TimeSpan.FromMilliseconds(500), new SourceHealthRegistry());
        var r = await runner.ExecuteAsync(1, default);
        Assert.Equal("p", r.SourceName);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(0, backup.CallCount);
    }

    [Fact]
    public async Task PrimarySlow_BackupHedgesAndWins()
    {
        var primary = new FakeSource("p", 0, TimeSpan.FromMilliseconds(2000));
        var backup = new FakeSource("b", 1, TimeSpan.FromMilliseconds(50));
        var runner = new HedgedRequestRunner<int, string>(
            new IDataSource<int, string>[] { primary, backup },
            "k", TimeSpan.FromMilliseconds(200), new SourceHealthRegistry());
        var r = await runner.ExecuteAsync(1, default);
        Assert.Equal("b", r.SourceName);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(1, backup.CallCount);
    }

    [Fact]
    public async Task PrimaryFails_BackupTakesOver()
    {
        var primary = new FakeSource("p", 0, TimeSpan.FromMilliseconds(20), error: new HttpRequestException("p-err"));
        var backup = new FakeSource("b", 1, TimeSpan.FromMilliseconds(20));
        var runner = new HedgedRequestRunner<int, string>(
            new IDataSource<int, string>[] { primary, backup },
            "k", TimeSpan.FromMilliseconds(500), new SourceHealthRegistry());
        var r = await runner.ExecuteAsync(2, default);
        Assert.Equal("b", r.SourceName);
        Assert.Equal("b:2", r.Value);
    }

    [Fact]
    public async Task AllFail_ThrowsAllSourcesFailed()
    {
        var a = new FakeSource("a", 0, TimeSpan.FromMilliseconds(10), error: new HttpRequestException("a"));
        var b = new FakeSource("b", 1, TimeSpan.FromMilliseconds(10), error: new HttpRequestException("b"));
        var runner = new HedgedRequestRunner<int, string>(
            new IDataSource<int, string>[] { a, b },
            "kind-x", TimeSpan.FromMilliseconds(50), new SourceHealthRegistry());
        var ex = await Assert.ThrowsAsync<AllSourcesFailedException>(async () => await runner.ExecuteAsync(0, default));
        Assert.Equal("kind-x", ex.DataKind);
        Assert.Equal(2, ex.InnerExceptions.Count);
    }

    [Fact]
    public async Task ExternalCancellation_Propagates()
    {
        var slow = new FakeSource("s", 0, TimeSpan.FromSeconds(5));
        var runner = new HedgedRequestRunner<int, string>(
            new[] { slow }, "k", TimeSpan.FromMilliseconds(500), new SourceHealthRegistry());
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await runner.ExecuteAsync(1, cts.Token));
    }

    [Fact]
    public async Task UnhealthySource_IsSkipped()
    {
        var health = new SourceHealthRegistry { FailureThreshold = 1, Cooldown = TimeSpan.FromMinutes(5) };
        health.MarkFailure("p", new Exception());
        var primary = new FakeSource("p", 0, TimeSpan.FromMilliseconds(10));
        var backup = new FakeSource("b", 1, TimeSpan.FromMilliseconds(10));
        var runner = new HedgedRequestRunner<int, string>(
            new IDataSource<int, string>[] { primary, backup },
            "k", TimeSpan.FromMilliseconds(500), health);
        var r = await runner.ExecuteAsync(3, default);
        Assert.Equal("b", r.SourceName);
        Assert.Equal(0, primary.CallCount);
        Assert.Equal(1, backup.CallCount);
    }

    [Fact]
    public async Task Winner_CancelsLoser()
    {
        var primary = new FakeSource("p", 0, TimeSpan.FromMilliseconds(20));
        var backup = new FakeSource("b", 1, TimeSpan.FromSeconds(5));
        // primary 慢 → backup 启动 → primary 早完成；不太对，反过来：让 primary 极慢
        primary = new FakeSource("p", 0, TimeSpan.FromSeconds(5));
        backup = new FakeSource("b", 1, TimeSpan.FromMilliseconds(20));
        var runner = new HedgedRequestRunner<int, string>(
            new IDataSource<int, string>[] { primary, backup },
            "k", TimeSpan.FromMilliseconds(50), new SourceHealthRegistry());
        var r = await runner.ExecuteAsync(0, default);
        Assert.Equal("b", r.SourceName);
        // primary 在收到取消后应较快退出（FakeSource 用 ct 取消的 Task.Delay）
        // 给它一点时间宣告取消已传播
        await Task.Delay(200);
        Assert.True(primary.LastToken.IsCancellationRequested, "primary token must be cancelled by runner");
    }
}
