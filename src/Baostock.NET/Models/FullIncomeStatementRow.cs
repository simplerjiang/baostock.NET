namespace Baostock.NET.Models;

/// <summary>
/// 利润表单期完整数据（来自东财 lrbAjaxNew 或新浪 lrb）。货币单位：元。
/// </summary>
public sealed record FullIncomeStatementRow
{
    /// <summary>东财风格证券代码，如 "SH600519"。</summary>
    public required string Code { get; init; }

    /// <summary>报告期截止日（如 2024-12-31）。</summary>
    public required DateOnly ReportDate { get; init; }

    /// <summary>报告类型标识（如 "一季报"/"半年报"/"三季报"/"年报"）。</summary>
    public string? ReportTitle { get; init; }

    /// <summary>营业总收入。</summary>
    public decimal? TotalOperateIncome { get; init; }

    /// <summary>营业收入。</summary>
    public decimal? OperateIncome { get; init; }

    /// <summary>营业总成本。</summary>
    public decimal? TotalOperateCost { get; init; }

    /// <summary>营业成本。</summary>
    public decimal? OperateCost { get; init; }

    /// <summary>销售费用。</summary>
    public decimal? SaleExpense { get; init; }

    /// <summary>管理费用。</summary>
    public decimal? ManageExpense { get; init; }

    /// <summary>研发费用。</summary>
    public decimal? ResearchExpense { get; init; }

    /// <summary>财务费用。</summary>
    public decimal? FinanceExpense { get; init; }

    /// <summary>营业利润。</summary>
    public decimal? OperateProfit { get; init; }

    /// <summary>利润总额。</summary>
    public decimal? TotalProfit { get; init; }

    /// <summary>所得税。</summary>
    public decimal? IncomeTax { get; init; }

    /// <summary>净利润。</summary>
    public decimal? NetProfit { get; init; }

    /// <summary>归属母公司股东净利润。</summary>
    public decimal? ParentNetProfit { get; init; }

    /// <summary>基本每股收益。</summary>
    public decimal? BasicEps { get; init; }

    /// <summary>稀释每股收益。</summary>
    public decimal? DilutedEps { get; init; }

    /// <summary>原始响应字段（所有 key-value 对），方便用户访问未建模字段。</summary>
    public IReadOnlyDictionary<string, string?>? RawFields { get; init; }

    /// <summary>数据源名称："EastMoney" / "Sina"。</summary>
    public required string Source { get; init; }
}
