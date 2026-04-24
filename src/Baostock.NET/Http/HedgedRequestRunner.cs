namespace Baostock.NET.Http;

/// <summary>
/// 对冲调度结果：包含返回值与胜出的数据源名称。
/// </summary>
/// <typeparam name="T">结果类型。</typeparam>
/// <param name="Value">数据源返回的结果。</param>
/// <param name="SourceName">胜出数据源的名称。</param>
public sealed record HedgedResult<T>(T Value, string SourceName);

/// <summary>
/// 单次结果对冲调度器：按优先级启动数据源，每隔 <c>hedgeInterval</c> 启动下一个备源；
/// 第一个成功者胜出，胜出后取消其它在途请求。详见 <c>docs/v1.2.0-plan.md §3.3</c>。
/// </summary>
/// <typeparam name="TRequest">请求参数类型。</typeparam>
/// <typeparam name="TResult">返回结果类型。</typeparam>
public sealed class HedgedRequestRunner<TRequest, TResult>
{
    private readonly IReadOnlyList<IDataSource<TRequest, TResult>> _sources;
    private readonly string _dataKind;
    private readonly TimeSpan _hedgeInterval;
    private readonly SourceHealthRegistry _health;

    /// <summary>构造一个对冲调度器。</summary>
    /// <param name="sources">所有候选数据源。</param>
    /// <param name="dataKind">数据种类标识，用于异常聚合。</param>
    /// <param name="hedgeInterval">备源启动间隔。</param>
    /// <param name="health">健康注册表，缺省使用 <see cref="SourceHealthRegistry.Default"/>。</param>
    public HedgedRequestRunner(
        IReadOnlyList<IDataSource<TRequest, TResult>> sources,
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

    /// <summary>执行对冲调度，返回首个成功结果。</summary>
    /// <param name="request">请求参数。</param>
    /// <param name="cancellationToken">外部取消令牌。</param>
    /// <returns>含值与胜出源名称的 <see cref="HedgedResult{T}"/>。</returns>
    public async Task<HedgedResult<TResult>> ExecuteAsync(TRequest request, CancellationToken cancellationToken)
    {
        var candidates = SelectCandidates();

        var srcCts = new CancellationTokenSource?[candidates.Count];
        var tasks = new Task<TResult>?[candidates.Count];
        var failures = new List<Exception>(candidates.Count);
        int started = 0;

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

                var active = new List<Task<TResult>>();
                var activeIdx = new List<int>();
                for (int i = 0; i < tasks.Length; i++)
                {
                    if (tasks[i] != null)
                    {
                        active.Add(tasks[i]!);
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
                hedgeCts.Cancel(); // 释放未触发的 hedgeTask

                if (completed == hedgeTask)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (moreToStart)
                    {
                        StartNext();
                    }
                    continue;
                }

                int taskPos = active.IndexOf((Task<TResult>)completed);
                int idx = activeIdx[taskPos];
                tasks[idx] = null;

                try
                {
                    TResult value = await ((Task<TResult>)completed).ConfigureAwait(false);
                    _health.MarkSuccess(candidates[idx].Name);

                    // 取消并优雅等待 loser
                    var losers = new List<Task<TResult>>();
                    for (int i = 0; i < tasks.Length; i++)
                    {
                        if (tasks[i] != null)
                        {
                            srcCts[i]?.Cancel();
                            losers.Add(tasks[i]!);
                            tasks[i] = null;
                        }
                    }
                    if (losers.Count > 0)
                    {
                        try
                        {
                            await Task.WhenAll(losers).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                        }
                        catch
                        {
                            // 忽略 loser 的异常与等待超时
                        }
                        // 即使等待超时，也观察异常以避免 unobserved task exception
                        foreach (var lt in losers)
                        {
                            _ = lt.ContinueWith(t => GC.KeepAlive(t.Exception), TaskScheduler.Default);
                        }
                    }

                    return new HedgedResult<TResult>(value, candidates[idx].Name);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _health.MarkFailure(candidates[idx].Name, ex);
                    failures.Add(ex);
                    // 该源失败：若仍有未启动源且当前活动列表空，立刻启动下一个以保持响应
                    bool noneActive = tasks.All(t => t == null);
                    if (noneActive && started < candidates.Count)
                    {
                        StartNext();
                    }
                    continue;
                }
            }
        }
        finally
        {
            for (int i = 0; i < srcCts.Length; i++)
            {
                try { srcCts[i]?.Cancel(); } catch { }
                srcCts[i]?.Dispose();
            }
        }

        void StartNext()
        {
            int idx = started++;
            var src = candidates[idx];
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            srcCts[idx] = cts;
            // 用 Task.Run 隔离同步异常，确保即使 FetchAsync 当场抛同步异常也作为 Task 失败处理
            tasks[idx] = Task.Run(() => src.FetchAsync(request, cts.Token), CancellationToken.None);
        }
    }

    private List<IDataSource<TRequest, TResult>> SelectCandidates()
    {
        var healthy = _sources.Where(s => _health.IsHealthy(s.Name)).OrderBy(s => s.Priority).ToList();
        if (healthy.Count == 0)
        {
            return _sources.OrderBy(s => s.Priority).ToList();
        }
        return healthy;
    }
}
