namespace Baostock.NET.Http;

/// <summary>
/// 表示单个 HTTP 数据源在抓取过程中失败的异常。
/// 通常被 <see cref="AllSourcesFailedException"/> 聚合后抛出。
/// </summary>
public sealed class DataSourceException : Exception
{
    /// <summary>失败的数据源名称（与 <see cref="IDataSource{TRequest, TResult}.Name"/> 一致）。</summary>
    public string SourceName { get; }

    /// <summary>HTTP 状态码，如未发生 HTTP 响应（连接异常等）则为 <see langword="null"/>。</summary>
    public int? StatusCode { get; }

    /// <summary>使用指定信息构造异常。</summary>
    /// <param name="sourceName">数据源名称。</param>
    /// <param name="message">错误描述。</param>
    /// <param name="statusCode">HTTP 状态码，可选。</param>
    /// <param name="inner">内部异常，可选。</param>
    public DataSourceException(string sourceName, string message, int? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        SourceName = sourceName;
        StatusCode = statusCode;
    }
}

/// <summary>
/// 表示对冲调度中所有候选数据源全部失败时抛出的聚合异常。
/// </summary>
public sealed class AllSourcesFailedException : AggregateException
{
    /// <summary>数据种类（如 <c>"realtime"</c>、<c>"kline"</c>、<c>"balanceSheet"</c>）。</summary>
    public string DataKind { get; }

    /// <summary>使用指定数据种类与所有源的失败列表构造异常。</summary>
    /// <param name="dataKind">数据种类标识。</param>
    /// <param name="innerExceptions">每个数据源的失败异常集合。</param>
    public AllSourcesFailedException(string dataKind, IEnumerable<Exception> innerExceptions)
        : base($"All data sources for '{dataKind}' failed.", innerExceptions)
    {
        DataKind = dataKind;
    }
}
