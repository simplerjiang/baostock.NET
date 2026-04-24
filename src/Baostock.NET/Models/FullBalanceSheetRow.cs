namespace Baostock.NET.Models;

/// <summary>
/// 资产负债表单期完整数据（来自东财 zcfzbAjaxNew 或新浪 fzb）。
/// 字段随公司类型（一般/银行/保险/证券）不同而差异，所有非核心字段可能为 null。
/// 货币单位：元（CNY）。
/// </summary>
public sealed record FullBalanceSheetRow
{
    /// <summary>东财风格证券代码，如 "SH600519"。</summary>
    public required string Code { get; init; }

    /// <summary>报告期截止日（如 2024-12-31）。</summary>
    public required DateOnly ReportDate { get; init; }

    /// <summary>报告类型标识（如 "一季报"/"半年报"/"三季报"/"年报"）。</summary>
    public string? ReportTitle { get; init; }

    // ==================== 资产类（一般工商业核心字段） ====================

    /// <summary>货币资金。</summary>
    public decimal? MoneyCap { get; init; }

    /// <summary>交易性金融资产。</summary>
    public decimal? TradeFinassetNotfvtpl { get; init; }

    /// <summary>应收账款。</summary>
    public decimal? AccountsRece { get; init; }

    /// <summary>预付款项。</summary>
    public decimal? PrepaymentRece { get; init; }

    /// <summary>存货。</summary>
    public decimal? Inventory { get; init; }

    /// <summary>流动资产合计。</summary>
    public decimal? TotalCurrentAssets { get; init; }

    /// <summary>固定资产。</summary>
    public decimal? FixedAsset { get; init; }

    /// <summary>在建工程。</summary>
    public decimal? CipTotal { get; init; }

    /// <summary>无形资产。</summary>
    public decimal? IntangibleAsset { get; init; }

    /// <summary>非流动资产合计。</summary>
    public decimal? TotalNoncurrentAssets { get; init; }

    /// <summary>资产总计。</summary>
    public decimal? TotalAssets { get; init; }

    // ==================== 负债类 ====================

    /// <summary>短期借款。</summary>
    public decimal? ShortLoan { get; init; }

    /// <summary>应付账款。</summary>
    public decimal? AccountsPayable { get; init; }

    /// <summary>预收款项。</summary>
    public decimal? PredictLiab { get; init; }

    /// <summary>流动负债合计。</summary>
    public decimal? TotalCurrentLiab { get; init; }

    /// <summary>长期借款。</summary>
    public decimal? LongLoan { get; init; }

    /// <summary>非流动负债合计。</summary>
    public decimal? TotalNoncurrentLiab { get; init; }

    /// <summary>负债合计。</summary>
    public decimal? TotalLiabilities { get; init; }

    // ==================== 所有者权益 ====================

    /// <summary>实收资本（或股本）。</summary>
    public decimal? ShareCapital { get; init; }

    /// <summary>资本公积。</summary>
    public decimal? CapitalReserve { get; init; }

    /// <summary>盈余公积。</summary>
    public decimal? SurplusReserve { get; init; }

    /// <summary>未分配利润。</summary>
    public decimal? UnassignProfit { get; init; }

    /// <summary>归属母公司所有者权益合计。</summary>
    public decimal? TotalParentEquity { get; init; }

    /// <summary>所有者权益合计。</summary>
    public decimal? TotalEquity { get; init; }

    // ==================== 元信息 ====================

    /// <summary>原始响应字段（所有 key-value 对），方便用户访问未建模字段。</summary>
    public IReadOnlyDictionary<string, string?>? RawFields { get; init; }

    /// <summary>数据源名称："EastMoney" / "Sina"。</summary>
    public required string Source { get; init; }
}
