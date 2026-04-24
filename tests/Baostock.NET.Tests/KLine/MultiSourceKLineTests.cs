using Baostock.NET.Http;
using Baostock.NET.KLine;
using Baostock.NET.Models;

namespace Baostock.NET.Tests.KLine;

internal sealed class FakeKLineSource : IDataSource<KLineRequest, IReadOnlyList<EastMoneyKLineRow>>
{
    private readonly TimeSpan _delay;
    private readonly Func<KLineRequest, IReadOnlyList<EastMoneyKLineRow>>? _result;
    private readonly Exception? _error;
    public int CallCount;
    public KLineRequest? LastRequest;
    public CancellationToken LastToken;

    public FakeKLineSource(string name, int priority, TimeSpan delay,
        Func<KLineRequest, IReadOnlyList<EastMoneyKLineRow>>? result = null,
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

    public async Task<IReadOnlyList<EastMoneyKLineRow>> FetchAsync(KLineRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref CallCount);
        LastRequest = request;
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

public class MultiSourceKLineTests
{
    private static EastMoneyKLineRow MakeRow(string code, string source, decimal close) =>
        new(code, new DateTime(2026, 4, 23), 100m, close, 110m, 90m,
            1000L, null, null, null, null, null, source);

    private static KLineRequest SampleReq(AdjustFlag adjust = AdjustFlag.PreAdjust) =>
        new("SH600519", KLineFrequency.Day,
            new DateTime(2026, 3, 1), new DateTime(2026, 4, 24), adjust);

    [Fact]
    public async Task EM_Fail_FallbackToTencent()
    {
        var em = new FakeKLineSource("EastMoney", 0, TimeSpan.Zero,
            error: new DataSourceException("EastMoney", "EMPTY"));
        var tencent = new FakeKLineSource("Tencent", 1, TimeSpan.FromMilliseconds(20),
            result: r => new[] { MakeRow(r.Code, "Tencent", 1413.64m) });

        var runner = new HedgedRequestRunner<KLineRequest, IReadOnlyList<EastMoneyKLineRow>>(
            new IDataSource<KLineRequest, IReadOnlyList<EastMoneyKLineRow>>[] { em, tencent },
            "kline", TimeSpan.FromMilliseconds(500), new SourceHealthRegistry());

        var result = await runner.ExecuteAsync(SampleReq(), default);
        Assert.Equal("Tencent", result.SourceName);
        Assert.Equal("Tencent", result.Value[0].Source);
        Assert.Equal(1, em.CallCount);
        Assert.Equal(1, tencent.CallCount);
    }

    [Fact]
    public async Task HedgeWinner_CancelsLosers()
    {
        // EM 慢（800ms）；hedge 100ms 后启动 Tencent（20ms 完成）→ Tencent 胜出 → EM 应被取消。
        var em = new FakeKLineSource("EastMoney", 0, TimeSpan.FromMilliseconds(800),
            result: _ => Array.Empty<EastMoneyKLineRow>());
        var tencent = new FakeKLineSource("Tencent", 1, TimeSpan.FromMilliseconds(20),
            result: r => new[] { MakeRow(r.Code, "Tencent", 1m) });

        var runner = new HedgedRequestRunner<KLineRequest, IReadOnlyList<EastMoneyKLineRow>>(
            new IDataSource<KLineRequest, IReadOnlyList<EastMoneyKLineRow>>[] { em, tencent },
            "kline", TimeSpan.FromMilliseconds(100), new SourceHealthRegistry());

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await runner.ExecuteAsync(SampleReq(), default);
        sw.Stop();

        Assert.Equal("Tencent", result.SourceName);
        await Task.Delay(50);
        Assert.True(em.LastToken.IsCancellationRequested, "Loser source token should be canceled.");
        Assert.True(sw.ElapsedMilliseconds < 600, $"Hedge should win quickly, took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task AllSourcesFail_ThrowsAggregate()
    {
        var em = new FakeKLineSource("EastMoney", 0, TimeSpan.Zero,
            error: new DataSourceException("EastMoney", "EMPTY"));
        var tencent = new FakeKLineSource("Tencent", 1, TimeSpan.Zero,
            error: new DataSourceException("Tencent", "EMPTY"));

        var runner = new HedgedRequestRunner<KLineRequest, IReadOnlyList<EastMoneyKLineRow>>(
            new IDataSource<KLineRequest, IReadOnlyList<EastMoneyKLineRow>>[] { em, tencent },
            "kline", TimeSpan.FromMilliseconds(500), new SourceHealthRegistry());

        var ex = await Assert.ThrowsAsync<AllSourcesFailedException>(
            () => runner.ExecuteAsync(SampleReq(), default));
        Assert.Equal("kline", ex.DataKind);
        Assert.Equal(2, ex.InnerExceptions.Count);
    }

    [Fact]
    public async Task AdjustFlag_PassesThrough_ToSource()
    {
        // 验证 AdjustFlag 被原样传到胜出源（不会被 hedge runner 改写）。
        var em = new FakeKLineSource("EastMoney", 0, TimeSpan.FromMilliseconds(10),
            result: r => new[] { MakeRow(r.Code, "EastMoney", 1m) });
        var tencent = new FakeKLineSource("Tencent", 1, TimeSpan.Zero,
            result: r => new[] { MakeRow(r.Code, "Tencent", 1m) });

        var runner = new HedgedRequestRunner<KLineRequest, IReadOnlyList<EastMoneyKLineRow>>(
            new IDataSource<KLineRequest, IReadOnlyList<EastMoneyKLineRow>>[] { em, tencent },
            "kline", TimeSpan.FromMilliseconds(500), new SourceHealthRegistry());

        await runner.ExecuteAsync(SampleReq(AdjustFlag.PostAdjust), default);
        Assert.NotNull(em.LastRequest);
        Assert.Equal(AdjustFlag.PostAdjust, em.LastRequest!.Adjust);
        Assert.Equal("SH600519", em.LastRequest.Code);
        Assert.Equal(KLineFrequency.Day, em.LastRequest.Frequency);
    }
}
