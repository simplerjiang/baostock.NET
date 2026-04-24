using System.Runtime.CompilerServices;
using Baostock.NET.Http;

namespace Baostock.NET.Tests.Http;

internal sealed class FakeStreamSource : IStreamDataSource<int, int>
{
    private readonly TimeSpan _firstDelay;
    private readonly TimeSpan _itemDelay;
    private readonly int _count;
    private readonly Exception? _firstError;
    public int CallCount;
    public CancellationToken LastToken;
    public bool Completed;

    public FakeStreamSource(string name, int priority, TimeSpan firstDelay, int count, TimeSpan? itemDelay = null, Exception? firstError = null)
    {
        Name = name;
        Priority = priority;
        _firstDelay = firstDelay;
        _itemDelay = itemDelay ?? TimeSpan.FromMilliseconds(5);
        _count = count;
        _firstError = firstError;
    }

    public string Name { get; }
    public int Priority { get; }

    public async IAsyncEnumerable<int> StreamAsync(int request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref CallCount);
        LastToken = cancellationToken;
        if (_firstDelay > TimeSpan.Zero)
        {
            await Task.Delay(_firstDelay, cancellationToken).ConfigureAwait(false);
        }
        if (_firstError != null)
        {
            throw _firstError;
        }
        for (int i = 0; i < _count; i++)
        {
            yield return request * 100 + i;
            if (i < _count - 1 && _itemDelay > TimeSpan.Zero)
            {
                await Task.Delay(_itemDelay, cancellationToken).ConfigureAwait(false);
            }
        }
        Completed = true;
    }
}

public class StreamingHedgedRunnerTests
{
    [Fact]
    public async Task SingleSource_StreamsAll()
    {
        var src = new FakeStreamSource("a", 0, TimeSpan.FromMilliseconds(10), 3);
        var runner = new StreamingHedgedRunner<int, int>(
            new[] { src }, "k", TimeSpan.FromMilliseconds(500), new SourceHealthRegistry());
        var collected = new List<int>();
        await foreach (var v in runner.ExecuteAsync(2, default))
        {
            collected.Add(v);
        }
        Assert.Equal(new[] { 200, 201, 202 }, collected);
    }

    [Fact]
    public async Task PrimarySlow_BackupHedgesAndWins()
    {
        var primary = new FakeStreamSource("p", 0, TimeSpan.FromSeconds(5), 3);
        var backup = new FakeStreamSource("b", 1, TimeSpan.FromMilliseconds(20), 2);
        var runner = new StreamingHedgedRunner<int, int>(
            new IStreamDataSource<int, int>[] { primary, backup },
            "k", TimeSpan.FromMilliseconds(100), new SourceHealthRegistry());
        var collected = new List<int>();
        await foreach (var v in runner.ExecuteAsync(1, default))
        {
            collected.Add(v);
        }
        Assert.Equal(new[] { 100, 101 }, collected);
        await Task.Delay(100);
        Assert.True(primary.LastToken.IsCancellationRequested);
    }

    [Fact]
    public async Task PrimaryFails_BackupTakesOver()
    {
        var primary = new FakeStreamSource("p", 0, TimeSpan.FromMilliseconds(20), 0, firstError: new HttpRequestException("err"));
        var backup = new FakeStreamSource("b", 1, TimeSpan.FromMilliseconds(20), 2);
        var runner = new StreamingHedgedRunner<int, int>(
            new IStreamDataSource<int, int>[] { primary, backup },
            "k", TimeSpan.FromMilliseconds(500), new SourceHealthRegistry());
        var collected = new List<int>();
        await foreach (var v in runner.ExecuteAsync(3, default))
        {
            collected.Add(v);
        }
        Assert.Equal(new[] { 300, 301 }, collected);
    }

    [Fact]
    public async Task AllFail_ThrowsAllSourcesFailed()
    {
        var a = new FakeStreamSource("a", 0, TimeSpan.FromMilliseconds(10), 0, firstError: new HttpRequestException("ax"));
        var b = new FakeStreamSource("b", 1, TimeSpan.FromMilliseconds(10), 0, firstError: new HttpRequestException("bx"));
        var runner = new StreamingHedgedRunner<int, int>(
            new IStreamDataSource<int, int>[] { a, b },
            "stream-x", TimeSpan.FromMilliseconds(50), new SourceHealthRegistry());
        await Assert.ThrowsAsync<AllSourcesFailedException>(async () =>
        {
            await foreach (var _ in runner.ExecuteAsync(0, default)) { }
        });
    }

    [Fact]
    public async Task ExternalCancellation_StopsEnumeration()
    {
        var src = new FakeStreamSource("a", 0, TimeSpan.FromMilliseconds(10), 100, itemDelay: TimeSpan.FromMilliseconds(50));
        var runner = new StreamingHedgedRunner<int, int>(
            new[] { src }, "k", TimeSpan.FromMilliseconds(500), new SourceHealthRegistry());
        using var cts = new CancellationTokenSource();
        int seen = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var v in runner.ExecuteAsync(0, cts.Token))
            {
                seen++;
                if (seen == 2) cts.Cancel();
            }
        });
        Assert.True(seen < 100);
    }

    [Fact]
    public async Task Winner_CancelsLoser()
    {
        var primary = new FakeStreamSource("p", 0, TimeSpan.FromSeconds(5), 3);
        var backup = new FakeStreamSource("b", 1, TimeSpan.FromMilliseconds(20), 2);
        var runner = new StreamingHedgedRunner<int, int>(
            new IStreamDataSource<int, int>[] { primary, backup },
            "k", TimeSpan.FromMilliseconds(50), new SourceHealthRegistry());
        await foreach (var _ in runner.ExecuteAsync(0, default)) { }
        await Task.Delay(200);
        Assert.True(primary.LastToken.IsCancellationRequested);
    }

    [Fact]
    public async Task UnhealthySource_IsSkipped()
    {
        var health = new SourceHealthRegistry { FailureThreshold = 1, Cooldown = TimeSpan.FromMinutes(5) };
        health.MarkFailure("p", new Exception());
        var primary = new FakeStreamSource("p", 0, TimeSpan.FromMilliseconds(5), 3);
        var backup = new FakeStreamSource("b", 1, TimeSpan.FromMilliseconds(5), 2);
        var runner = new StreamingHedgedRunner<int, int>(
            new IStreamDataSource<int, int>[] { primary, backup },
            "k", TimeSpan.FromMilliseconds(500), health);
        var collected = new List<int>();
        await foreach (var v in runner.ExecuteAsync(4, default))
        {
            collected.Add(v);
        }
        Assert.Equal(new[] { 400, 401 }, collected);
        Assert.Equal(0, primary.CallCount);
    }
}
