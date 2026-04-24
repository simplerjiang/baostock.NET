namespace Baostock.NET.Financials;

/// <summary>
/// 东方财富财务数据公司类型。决定使用哪一套财报结构解析。
/// </summary>
public enum CompanyType
{
    /// <summary>一般工商业（默认）。</summary>
    General = 4,

    /// <summary>银行。</summary>
    Bank = 1,

    /// <summary>保险。</summary>
    Insurance = 2,

    /// <summary>证券。</summary>
    Securities = 3,
}
