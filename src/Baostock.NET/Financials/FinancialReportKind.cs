namespace Baostock.NET.Financials;

/// <summary>
/// 财报口径（东财 reportType 参数）。
/// </summary>
public enum FinancialReportKind
{
    /// <summary>累计口径。</summary>
    Cumulative = 1,

    /// <summary>单季度口径。</summary>
    SingleQuarter = 2,
}
