namespace Baostock.NET.Http;

/// <summary>
/// 表示一个一次性返回结果的 HTTP 数据源（如新浪行情、东方财富快照等）。
/// </summary>
/// <typeparam name="TRequest">请求参数类型。</typeparam>
/// <typeparam name="TResult">返回结果类型。</typeparam>
public interface IDataSource<TRequest, TResult>
{
    /// <summary>数据源名称（用于健康统计与日志）。</summary>
    string Name { get; }

    /// <summary>优先级，数值越小越优先被尝试。</summary>
    int Priority { get; }

    /// <summary>从该数据源抓取一次结果。</summary>
    /// <param name="request">请求参数。</param>
    /// <param name="cancellationToken">取消令牌（对冲调度可能在选定胜者后取消其它源）。</param>
    /// <returns>抓取结果。</returns>
    Task<TResult> FetchAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// 表示一个流式返回结果的 HTTP 数据源（如分笔成交流式拉取）。
/// </summary>
/// <typeparam name="TRequest">请求参数类型。</typeparam>
/// <typeparam name="TResult">每个流元素的类型。</typeparam>
public interface IStreamDataSource<TRequest, TResult>
{
    /// <summary>数据源名称（用于健康统计与日志）。</summary>
    string Name { get; }

    /// <summary>优先级，数值越小越优先被尝试。</summary>
    int Priority { get; }

    /// <summary>开始流式抓取。</summary>
    /// <param name="request">请求参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步可枚举结果序列。</returns>
    IAsyncEnumerable<TResult> StreamAsync(TRequest request, CancellationToken cancellationToken);
}
