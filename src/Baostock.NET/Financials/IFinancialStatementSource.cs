using Baostock.NET.Models;

namespace Baostock.NET.Financials;

/// <summary>
/// 财务报表数据源抽象。三张报表按独立方法暴露，便于各源独立降级。
/// </summary>
public interface IFinancialStatementSource
{
    /// <summary>源标识名，如 "EastMoney"、"Sina"。</summary>
    string Name { get; }

    /// <summary>源优先级。数值越小优先级越高（0 为主源）。</summary>
    int Priority { get; }

    /// <summary>查询资产负债表。</summary>
    /// <param name="request">财报查询请求。</param>
    /// <param name="ct">取消令牌。</param>
    Task<IReadOnlyList<FullBalanceSheetRow>> GetBalanceSheetAsync(
        FinancialStatementRequest request,
        CancellationToken ct = default);

    /// <summary>查询利润表。</summary>
    /// <param name="request">财报查询请求。</param>
    /// <param name="ct">取消令牌。</param>
    Task<IReadOnlyList<FullIncomeStatementRow>> GetIncomeStatementAsync(
        FinancialStatementRequest request,
        CancellationToken ct = default);

    /// <summary>查询现金流量表。</summary>
    /// <param name="request">财报查询请求。</param>
    /// <param name="ct">取消令牌。</param>
    Task<IReadOnlyList<FullCashFlowRow>> GetCashFlowStatementAsync(
        FinancialStatementRequest request,
        CancellationToken ct = default);
}
