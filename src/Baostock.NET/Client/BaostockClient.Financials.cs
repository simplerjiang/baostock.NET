using Baostock.NET.Financials;
using Baostock.NET.Http;
using Baostock.NET.Models;

namespace Baostock.NET.Client;

/// <summary>
/// <see cref="BaostockClient"/> 财报三源对冲扩展（v1.3.0 Sprint 3）。
/// 资产负债表 / 利润表 / 现金流量表：EastMoney(0) → Sina(1)，hedge 间隔 500ms。
/// </summary>
public sealed partial class BaostockClient
{
    private static readonly TimeSpan DefaultFinancialHedgeInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// 查询完整资产负债表（东财 + 新浪双源对冲，hedge 间隔 500ms）。
    /// </summary>
    /// <param name="request">财报查询请求，不能为 <see langword="null"/>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>资产负债表行集合（按报告期降序，具体取决于胜出源）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="AllSourcesFailedException">东财 + 新浪双源全部失败。</exception>
    public async Task<IReadOnlyList<FullBalanceSheetRow>> QueryFullBalanceSheetAsync(
        FinancialStatementRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sources = DefaultBalanceSheetSources();
        var runner = new HedgedRequestRunner<FinancialStatementRequest, IReadOnlyList<FullBalanceSheetRow>>(
            sources,
            dataKind: "financial-balance",
            hedgeInterval: DefaultFinancialHedgeInterval,
            health: SourceHealthRegistry.Default);
        var hedged = await runner.ExecuteAsync(request, ct).ConfigureAwait(false);
        return hedged.Value;
    }

    /// <summary>
    /// 查询完整利润表（东财 + 新浪双源对冲，hedge 间隔 500ms）。
    /// </summary>
    /// <param name="request">财报查询请求，不能为 <see langword="null"/>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>利润表行集合。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="AllSourcesFailedException">东财 + 新浪双源全部失败。</exception>
    public async Task<IReadOnlyList<FullIncomeStatementRow>> QueryFullIncomeStatementAsync(
        FinancialStatementRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sources = DefaultIncomeStatementSources();
        var runner = new HedgedRequestRunner<FinancialStatementRequest, IReadOnlyList<FullIncomeStatementRow>>(
            sources,
            dataKind: "financial-income",
            hedgeInterval: DefaultFinancialHedgeInterval,
            health: SourceHealthRegistry.Default);
        var hedged = await runner.ExecuteAsync(request, ct).ConfigureAwait(false);
        return hedged.Value;
    }

    /// <summary>
    /// 查询完整现金流量表（东财 + 新浪双源对冲，hedge 间隔 500ms）。
    /// </summary>
    /// <param name="request">财报查询请求，不能为 <see langword="null"/>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>现金流量表行集合。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="AllSourcesFailedException">东财 + 新浪双源全部失败。</exception>
    public async Task<IReadOnlyList<FullCashFlowRow>> QueryFullCashFlowAsync(
        FinancialStatementRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sources = DefaultCashFlowSources();
        var runner = new HedgedRequestRunner<FinancialStatementRequest, IReadOnlyList<FullCashFlowRow>>(
            sources,
            dataKind: "financial-cashflow",
            hedgeInterval: DefaultFinancialHedgeInterval,
            health: SourceHealthRegistry.Default);
        var hedged = await runner.ExecuteAsync(request, ct).ConfigureAwait(false);
        return hedged.Value;
    }

    private static IReadOnlyList<IDataSource<FinancialStatementRequest, IReadOnlyList<FullBalanceSheetRow>>> DefaultBalanceSheetSources()
        => new IDataSource<FinancialStatementRequest, IReadOnlyList<FullBalanceSheetRow>>[]
        {
            new FinancialStatementSourceAdapter<FullBalanceSheetRow>(
                new EastMoneyBalanceSheetSource(),
                static (s, r, t) => s.GetBalanceSheetAsync(r, t)),
            new FinancialStatementSourceAdapter<FullBalanceSheetRow>(
                new SinaBalanceSheetSource(),
                static (s, r, t) => s.GetBalanceSheetAsync(r, t)),
        };

    private static IReadOnlyList<IDataSource<FinancialStatementRequest, IReadOnlyList<FullIncomeStatementRow>>> DefaultIncomeStatementSources()
        => new IDataSource<FinancialStatementRequest, IReadOnlyList<FullIncomeStatementRow>>[]
        {
            new FinancialStatementSourceAdapter<FullIncomeStatementRow>(
                new EastMoneyIncomeStatementSource(),
                static (s, r, t) => s.GetIncomeStatementAsync(r, t)),
            new FinancialStatementSourceAdapter<FullIncomeStatementRow>(
                new SinaIncomeStatementSource(),
                static (s, r, t) => s.GetIncomeStatementAsync(r, t)),
        };

    private static IReadOnlyList<IDataSource<FinancialStatementRequest, IReadOnlyList<FullCashFlowRow>>> DefaultCashFlowSources()
        => new IDataSource<FinancialStatementRequest, IReadOnlyList<FullCashFlowRow>>[]
        {
            new FinancialStatementSourceAdapter<FullCashFlowRow>(
                new EastMoneyCashFlowSource(),
                static (s, r, t) => s.GetCashFlowStatementAsync(r, t)),
            new FinancialStatementSourceAdapter<FullCashFlowRow>(
                new SinaCashFlowSource(),
                static (s, r, t) => s.GetCashFlowStatementAsync(r, t)),
        };

    /// <summary>
    /// 把非泛型 <see cref="IFinancialStatementSource"/> 适配为 <see cref="IDataSource{TRequest, TResult}"/>，
    /// 以便喂给 <see cref="HedgedRequestRunner{TRequest, TResult}"/>。不改源类本身。
    /// </summary>
    private sealed class FinancialStatementSourceAdapter<TRow>
        : IDataSource<FinancialStatementRequest, IReadOnlyList<TRow>>
    {
        private readonly IFinancialStatementSource _inner;
        private readonly Func<IFinancialStatementSource, FinancialStatementRequest, CancellationToken, Task<IReadOnlyList<TRow>>> _invoke;

        public FinancialStatementSourceAdapter(
            IFinancialStatementSource inner,
            Func<IFinancialStatementSource, FinancialStatementRequest, CancellationToken, Task<IReadOnlyList<TRow>>> invoke)
        {
            _inner = inner;
            _invoke = invoke;
        }

        public string Name => _inner.Name;

        public int Priority => _inner.Priority;

        public Task<IReadOnlyList<TRow>> FetchAsync(FinancialStatementRequest request, CancellationToken cancellationToken)
            => _invoke(_inner, request, cancellationToken);
    }
}
