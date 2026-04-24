namespace Baostock.NET.Models;

/// <summary>
/// 现金流量表单期完整数据（来自东财 xjllbAjaxNew 或新浪 llb）。货币单位：元。
/// </summary>
public sealed record FullCashFlowRow
{
    /// <summary>东财风格证券代码，如 "SH600519"。</summary>
    public required string Code { get; init; }

    /// <summary>报告期截止日（如 2024-12-31）。</summary>
    public required DateOnly ReportDate { get; init; }

    /// <summary>报告类型标识（如 "一季报"/"半年报"/"三季报"/"年报"）。</summary>
    public string? ReportTitle { get; init; }

    // ==================== 经营活动 ====================

    /// <summary>销售商品、提供劳务收到的现金。</summary>
    public decimal? SalesServices { get; init; }

    /// <summary>经营活动现金流入小计。</summary>
    public decimal? TotalOperateInflow { get; init; }

    /// <summary>经营活动现金流出小计。</summary>
    public decimal? TotalOperateOutflow { get; init; }

    /// <summary>经营活动产生的现金流量净额。</summary>
    public decimal? NetcashOperate { get; init; }

    // ==================== 投资活动 ====================

    /// <summary>投资活动现金流入小计。</summary>
    public decimal? TotalInvestInflow { get; init; }

    /// <summary>投资活动现金流出小计。</summary>
    public decimal? TotalInvestOutflow { get; init; }

    /// <summary>投资活动产生的现金流量净额。</summary>
    public decimal? NetcashInvest { get; init; }

    // ==================== 筹资活动 ====================

    /// <summary>筹资活动现金流入小计。</summary>
    public decimal? TotalFinanceInflow { get; init; }

    /// <summary>筹资活动现金流出小计。</summary>
    public decimal? TotalFinanceOutflow { get; init; }

    /// <summary>筹资活动产生的现金流量净额。</summary>
    public decimal? NetcashFinance { get; init; }

    // ==================== 期末现金 ====================

    /// <summary>期初现金及现金等价物余额。</summary>
    public decimal? BeginCce { get; init; }

    /// <summary>期末现金及现金等价物余额。</summary>
    public decimal? EndCce { get; init; }

    /// <summary>原始响应字段（所有 key-value 对），方便用户访问未建模字段。</summary>
    public IReadOnlyDictionary<string, string?>? RawFields { get; init; }

    /// <summary>数据源名称："EastMoney" / "Sina"。</summary>
    public required string Source { get; init; }
}
