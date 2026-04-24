using Baostock.NET.Http;
using Baostock.NET.KLine;
using Baostock.NET.Models;
using Baostock.NET.Realtime;

namespace Baostock.NET.Client;

/// <summary>
/// <see cref="BaostockClient"/> 多源对冲扩展（v1.2.0 Sprint 2）。
/// 实时行情：Sina(0) → Tencent(1) → EastMoney(2)，hedge 间隔 500ms。
/// 历史 K 线：EastMoney(0) → Tencent(1)，hedge 间隔 500ms。
/// </summary>
public sealed partial class BaostockClient
{
    private static readonly TimeSpan DefaultRealtimeHedgeInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan DefaultKLineHedgeInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// 获取单只股票的实时行情（三源对冲：Sina → Tencent → EastMoney，hedge 间隔 500ms）。
    /// </summary>
    /// <param name="code">东方财富风格代码（如 <c>SH600519</c>），亦兼容 <c>sh.600519</c> / <c>sh600519</c> / <c>1.600519</c>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>该股票的最新实时行情快照。</returns>
    /// <exception cref="AllSourcesFailedException">三源全部失败。</exception>
    public async Task<RealtimeQuote> GetRealtimeQuoteAsync(string code, CancellationToken ct = default)
    {
        var batch = await GetRealtimeQuotesAsync(new[] { code }, ct).ConfigureAwait(false);
        return batch[0];
    }

    /// <summary>
    /// 批量获取实时行情（三源对冲）。各源对入参 <paramref name="codes"/> 内部会翻译为对应风格。
    /// </summary>
    /// <param name="codes">东方财富风格代码集合。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>与入参 codes 顺序一致的行情列表。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="codes"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException"><paramref name="codes"/> 为空。</exception>
    /// <exception cref="AllSourcesFailedException">三源全部失败。</exception>
    public async Task<IReadOnlyList<RealtimeQuote>> GetRealtimeQuotesAsync(IEnumerable<string> codes, CancellationToken ct = default)
    {
        if (codes is null) throw new ArgumentNullException(nameof(codes));
        var arr = codes.ToArray();
        if (arr.Length == 0) throw new ArgumentException("codes must not be empty.", nameof(codes));

        var sources = DefaultRealtimeSources();
        var runner = new HedgedRequestRunner<string[], IReadOnlyList<RealtimeQuote>>(
            sources, dataKind: "realtime", hedgeInterval: DefaultRealtimeHedgeInterval, health: SourceHealthRegistry.Default);
        var hedged = await runner.ExecuteAsync(arr, ct).ConfigureAwait(false);
        return hedged.Value;
    }

    private static IReadOnlyList<IDataSource<string[], IReadOnlyList<RealtimeQuote>>> DefaultRealtimeSources()
        => new IDataSource<string[], IReadOnlyList<RealtimeQuote>>[]
        {
            new SinaRealtimeSource(),
            new TencentRealtimeSource(),
            new EastMoneyRealtimeSource(),
        };

    /// <summary>
    /// 获取历史 K 线（东财 + 腾讯双源对冲，hedge 间隔 500ms）。
    /// </summary>
    /// <remarks>
    /// <para>本方法与 <see cref="QueryHistoryKDataPlusAsync"/>（baostock TCP）<b>不同</b>：</para>
    /// <list type="bullet">
    ///   <item><description>走 HTTP，无需先 <c>Login</c>；</description></item>
    ///   <item><description>字段不含 PreClose/TradeStatus/IsST，但含振幅/换手率/涨跌额（东财胜出时）；</description></item>
    ///   <item><description>复权语义对齐东财：<see cref="AdjustFlag.PreAdjust"/> = 前复权（fqt=1 / qfq）；</description></item>
    ///   <item><description>分钟线（5/15/30/60）下，腾讯只返回原始数据，<see cref="AdjustFlag"/> 在 Tencent 路径上无效。</description></item>
    /// </list>
    /// </remarks>
    /// <param name="code">东方财富风格代码（亦兼容 <c>sh.600519</c>）。</param>
    /// <param name="frequency">K 线频率。</param>
    /// <param name="startDate">起始日期（含）。</param>
    /// <param name="endDate">结束日期（含）。</param>
    /// <param name="adjust">复权方式，缺省 <see cref="AdjustFlag.PreAdjust"/>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>K 线行集合，按时间升序。</returns>
    /// <exception cref="ArgumentException"><paramref name="code"/> 非法或日期区间无效。</exception>
    /// <exception cref="AllSourcesFailedException">东财 + 腾讯双源全部失败。</exception>
    public async Task<IReadOnlyList<EastMoneyKLineRow>> GetHistoryKLineAsync(
        string code,
        KLineFrequency frequency,
        DateTime startDate,
        DateTime endDate,
        AdjustFlag adjust = AdjustFlag.PreAdjust,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("code must not be empty.", nameof(code));
        if (endDate < startDate) throw new ArgumentException("endDate must be >= startDate.", nameof(endDate));

        var req = new KLineRequest(code, frequency, startDate, endDate, adjust);
        var sources = DefaultKLineSources();
        var runner = new HedgedRequestRunner<KLineRequest, IReadOnlyList<EastMoneyKLineRow>>(
            sources, dataKind: "kline", hedgeInterval: DefaultKLineHedgeInterval, health: SourceHealthRegistry.Default);
        var hedged = await runner.ExecuteAsync(req, ct).ConfigureAwait(false);
        return hedged.Value;
    }

    private static IReadOnlyList<IDataSource<KLineRequest, IReadOnlyList<EastMoneyKLineRow>>> DefaultKLineSources()
        => new IDataSource<KLineRequest, IReadOnlyList<EastMoneyKLineRow>>[]
        {
            new EastMoneyKLineSource(),
            new TencentKLineSource(),
        };
}
