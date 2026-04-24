namespace Baostock.NET.Financials;

/// <summary>
/// 财报日期汇总类型（东财 reportDateType 参数）。
/// </summary>
public enum FinancialReportDateType
{
    /// <summary>按报告期（累计口径，Q1/Q2/Q3/Q4）。</summary>
    ByReport = 0,

    /// <summary>按年度（仅年末数据）。</summary>
    ByYear = 1,

    /// <summary>按单季度（单 Q 口径）。</summary>
    BySingleQuarter = 2,
}
