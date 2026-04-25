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

    /// <summary>
    /// [⚠️ 已过时] 在 v1.3.x 中此字段语义在不同 source 间不一致：
    /// EastMoney 路径下原始字段 NETCASH_OPERATE 实为经营活动现金流量净额 (CFO)，
    /// Sina 路径下原始字段 CASHNETR 实为"现金及现金等价物净增加额"，两者数额可相差数倍。
    /// v1.4.0 起：此字段在两个 source 路径下统一指向"经营活动现金流量净额 (CFO)"，
    /// 与 <see cref="OperatingCashFlow"/> 同值。Sina 用户存在 BREAKING（v1.3.x 取的是净增加额，
    /// 现修正为 CFO），EastMoney 用户行为不变。
    /// 请改用语义清晰的新字段：
    /// <see cref="OperatingCashFlow"/>（经营活动现金流量净额, CFO）；
    /// <see cref="NetCashIncrease"/>（现金及现金等价物净增加额）。
    /// </summary>
    [Obsolete("v1.3.x 此字段语义跨 source 不一致 (EM=CFO, Sina=净增加额). v1.4.0 起统一为 CFO，与 OperatingCashFlow 同值. 请改用 OperatingCashFlow (CFO) 或 NetCashIncrease (净增加额).", false)]
    public decimal? NetcashOperate { get; init; }

    /// <summary>
    /// 现金及现金等价物净增加额（期末 - 期初），即经营+投资+筹资三类活动现金流的代数和。
    /// EastMoney 路径下由 NETCASH_OPERATE + NETCASH_INVEST + NETCASH_FINANCE 派生；
    /// Sina 路径下取原始字段 CASHNETR（若缺失则同样按三类合计派生）。
    /// </summary>
    public decimal? NetCashIncrease { get; init; }

    /// <summary>
    /// 经营活动产生的现金流量净额（Operating Cash Flow, CFO）。常用于现金流分析、自由现金流计算。
    /// EastMoney 路径下取原始字段 NETCASH_OPERATE（其语义即 CFO）；
    /// Sina 路径下取原始字段 MANANETR（备选 NETCFOPER）。
    /// </summary>
    public decimal? OperatingCashFlow { get; init; }

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
