namespace Baostock.NET.Http;

/// <summary>
/// 指数退避重试策略。
/// </summary>
public static class RetryPolicy
{
    /// <summary>
    /// 以指数退避方式重试 <paramref name="action"/>，初始延迟 <paramref name="initialDelay"/>，每次翻倍。
    /// 仅当 <paramref name="shouldRetry"/> 返回 <see langword="true"/> 且尚未达到 <paramref name="maxAttempts"/> 时才重试。
    /// 外部取消（<paramref name="cancellationToken"/>）一旦触发立刻向上传播。
    /// </summary>
    /// <typeparam name="T">返回值类型。</typeparam>
    /// <param name="action">要执行的异步动作，参数为内部传入的 ct。</param>
    /// <param name="maxAttempts">最大尝试次数（含首次）。须 ≥ 1。</param>
    /// <param name="initialDelay">首次失败后的等待时长。</param>
    /// <param name="shouldRetry">判定异常是否值得重试。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>动作返回值。</returns>
    public static async Task<T> WithExponentialBackoffAsync<T>(
        Func<CancellationToken, Task<T>> action,
        int maxAttempts,
        TimeSpan initialDelay,
        Func<Exception, bool> shouldRetry,
        CancellationToken cancellationToken)
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "maxAttempts must be >= 1");
        }
        var delay = initialDelay;
        Exception? last = null;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await action(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 取消异常一律不重试（无论是外部 ct 触发还是内部 timeout）
                throw;
            }
            catch (Exception ex) when (attempt < maxAttempts && shouldRetry(ex))
            {
                last = ex;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, TimeSpan.FromMinutes(1).TotalMilliseconds));
            }
        }
        // 不可达：上面循环要么 return 要么在最后一次抛出未被捕获
        throw last ?? new InvalidOperationException("RetryPolicy exhausted without exception.");
    }

    /// <summary>
    /// 默认应可重试判定：HTTP 请求异常、网络 I/O 异常返回 <see langword="true"/>；
    /// 任何取消异常一律不重试。
    /// </summary>
    /// <param name="ex">异常实例。</param>
    /// <returns>是否值得重试。</returns>
    public static bool DefaultShouldRetry(Exception ex) => ex switch
    {
        OperationCanceledException => false,
        HttpRequestException => true,
        IOException => true,
        _ => false,
    };
}
