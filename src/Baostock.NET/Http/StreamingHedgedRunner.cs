using System.Runtime.CompilerServices;

namespace Baostock.NET.Http;

/// <summary>
/// 流式对冲调度器：按优先级启动数据源，第一个成功产出 <b>第一个元素</b> 的源胜出，
/// 之后由该源独占完成剩余流；其它源被取消并 dispose。
/// 备注：胜出后中途断流不做补救（v1.2.0 不实现，TODO 留 v1.3）。
/// </summary>
/// <typeparam name="TRequest">请求参数类型。</typeparam>
/// <typeparam name="TResult">每个流元素类型。</typeparam>
public sealed class StreamingHedgedRunner<TRequest, TResult>
{
    private readonly IReadOnlyList<IStreamDataSource<TRequest, TResult>> _sources;
    private readonly string _dataKind;
    private readonly TimeSpan _hedgeInterval;
    private readonly SourceHealthRegistry _health;

    /// <summary>构造一个流式对冲调度器。</summary>
    /// <param name="sources">所有候选流式数据源。</param>
    /// <param name="dataKind">数据种类标识。</param>
    /// <param name="hedgeInterval">备源启动间隔。</param>
    /// <param name="health">健康注册表，缺省 <see cref="SourceHealthRegistry.Default"/>。</param>
    public StreamingHedgedRunner(
        IReadOnlyList<IStreamDataSource<TRequest, TResult>> sources,
        string dataKind,
        TimeSpan hedgeInterval,
        SourceHealthRegistry? health = null)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        if (sources.Count == 0)
        {
            throw new ArgumentException("At least one source is required.", nameof(sources));
        }
        _dataKind = dataKind ?? throw new ArgumentNullException(nameof(dataKind));
        _hedgeInterval = hedgeInterval;
        _health = health ?? SourceHealthRegistry.Default;
    }

    /// <summary>
    /// 执行流式对冲，返回胜出源的剩余元素。
    /// </summary>
    /// <param name="request">请求参数。</param>
    /// <param name="cancellationToken">外部取消令牌。</param>
    /// <returns>异步元素序列。</returns>
    public async IAsyncEnumerable<TResult> ExecuteAsync(
        TRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var raced = await RaceForFirstAsync(request, cancellationToken).ConfigureAwait(false);
        var winnerEnum = raced.WinnerEnum;
        try
        {
            yield return raced.FirstItem;
            while (true)
            {
                bool has;
                try
                {
                    has = await winnerEnum.MoveNextAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                if (!has)
                {
                    yield break;
                }
                yield return winnerEnum.Current;
            }
        }
        finally
        {
            try { await winnerEnum.DisposeAsync().ConfigureAwait(false); } catch { }
            raced.WinnerCts.Dispose();
        }
    }

    private sealed class RaceResult
    {
        public required IAsyncEnumerator<TResult> WinnerEnum { get; init; }
        public required TResult FirstItem { get; init; }
        public required CancellationTokenSource WinnerCts { get; init; }
    }

    private async Task<RaceResult> RaceForFirstAsync(TRequest request, CancellationToken cancellationToken)
    {
        var candidates = SelectCandidates();
        var srcCts = new CancellationTokenSource?[candidates.Count];
        var enums = new IAsyncEnumerator<TResult>?[candidates.Count];
        var moveNext = new Task<bool>?[candidates.Count];
        var failures = new List<Exception>(candidates.Count);
        int started = 0;
        bool winnerFound = false;
        int winnerIdx = -1;

        try
        {
            StartNext();

            while (true)
            {
                bool moreToStart = started < candidates.Count;
                using var hedgeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task hedgeTask = moreToStart
                    ? Task.Delay(_hedgeInterval, hedgeCts.Token)
                    : Task.Delay(Timeout.Infinite, hedgeCts.Token);

                var active = new List<Task<bool>>();
                var activeIdx = new List<int>();
                for (int i = 0; i < moveNext.Length; i++)
                {
                    if (moveNext[i] != null)
                    {
                        active.Add(moveNext[i]!);
                        activeIdx.Add(i);
                    }
                }

                if (active.Count == 0 && !moreToStart)
                {
                    throw new AllSourcesFailedException(_dataKind, failures);
                }

                var waitList = new List<Task>(active.Count + 1);
                waitList.AddRange(active);
                waitList.Add(hedgeTask);
                var completed = await Task.WhenAny(waitList).ConfigureAwait(false);
                hedgeCts.Cancel();

                if (completed == hedgeTask)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (moreToStart)
                    {
                        StartNext();
                    }
                    continue;
                }

                int taskPos = active.IndexOf((Task<bool>)completed);
                int idx = activeIdx[taskPos];
                moveNext[idx] = null;

                bool hasFirst;
                try
                {
                    hasFirst = await ((Task<bool>)completed).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _health.MarkFailure(candidates[idx].Name, ex);
                    failures.Add(ex);
                    await SafeDisposeAsync(enums[idx]).ConfigureAwait(false);
                    enums[idx] = null;
                    bool noneActive = moveNext.All(t => t == null);
                    if (noneActive && started < candidates.Count)
                    {
                        StartNext();
                    }
                    continue;
                }

                if (!hasFirst)
                {
                    // 空流：视为该源失败
                    var ex = new InvalidOperationException($"Source '{candidates[idx].Name}' produced no elements.");
                    _health.MarkFailure(candidates[idx].Name, ex);
                    failures.Add(ex);
                    await SafeDisposeAsync(enums[idx]).ConfigureAwait(false);
                    enums[idx] = null;
                    bool noneActive = moveNext.All(t => t == null);
                    if (noneActive && started < candidates.Count)
                    {
                        StartNext();
                    }
                    continue;
                }

                _health.MarkSuccess(candidates[idx].Name);
                winnerIdx = idx;
                winnerFound = true;
                var winnerEnum = enums[idx]!;
                var firstItem = winnerEnum.Current;
                enums[idx] = null;

                // 取消其它源
                for (int i = 0; i < enums.Length; i++)
                {
                    if (i == idx) continue;
                    try { srcCts[i]?.Cancel(); } catch { }
                }
                // 等待其它源 MoveNext 任务结束（避免 unobserved），不等太久
                var losers = new List<Task<bool>>();
                for (int i = 0; i < moveNext.Length; i++)
                {
                    if (moveNext[i] != null)
                    {
                        losers.Add(moveNext[i]!);
                        moveNext[i] = null;
                    }
                }
                if (losers.Count > 0)
                {
                    try { await Task.WhenAll(losers).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
                    catch { }
                    foreach (var lt in losers)
                    {
                        _ = lt.ContinueWith(t => GC.KeepAlive(t.Exception), TaskScheduler.Default);
                    }
                }
                // dispose loser 枚举器
                for (int i = 0; i < enums.Length; i++)
                {
                    if (enums[i] != null)
                    {
                        await SafeDisposeAsync(enums[i]).ConfigureAwait(false);
                        enums[i] = null;
                    }
                }

                return new RaceResult { WinnerEnum = winnerEnum, FirstItem = firstItem, WinnerCts = srcCts[idx]! };
            }
        }
        finally
        {
            // 失败路径：取消并 dispose 一切
            if (!winnerFound)
            {
                for (int i = 0; i < enums.Length; i++)
                {
                    try { srcCts[i]?.Cancel(); } catch { }
                    if (enums[i] != null)
                    {
                        await SafeDisposeAsync(enums[i]).ConfigureAwait(false);
                    }
                }
            }
            for (int i = 0; i < srcCts.Length; i++)
            {
                if (i == winnerIdx)
                {
                    // 胜者的 cts 由 ExecuteAsync 的 finally dispose（通过 RaceResult.WinnerCts）。
                    continue;
                }
                srcCts[i]?.Dispose();
            }
        }

        static async ValueTask SafeDisposeAsync(IAsyncEnumerator<TResult>? e)
        {
            if (e == null) return;
            try { await e.DisposeAsync().ConfigureAwait(false); } catch { }
        }

        void StartNext()
        {
            int idx = started++;
            var src = candidates[idx];
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            srcCts[idx] = cts;
            try
            {
                var en = src.StreamAsync(request, cts.Token).GetAsyncEnumerator(cts.Token);
                enums[idx] = en;
                moveNext[idx] = en.MoveNextAsync().AsTask();
            }
            catch (Exception ex)
            {
                _health.MarkFailure(src.Name, ex);
                failures.Add(ex);
                moveNext[idx] = null;
                enums[idx] = null;
            }
        }
    }

    private List<IStreamDataSource<TRequest, TResult>> SelectCandidates()
    {
        var healthy = _sources.Where(s => _health.IsHealthy(s.Name)).OrderBy(s => s.Priority).ToList();
        if (healthy.Count == 0)
        {
            return _sources.OrderBy(s => s.Priority).ToList();
        }
        return healthy;
    }
}
