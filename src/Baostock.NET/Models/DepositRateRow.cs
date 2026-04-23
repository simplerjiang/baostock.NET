namespace Baostock.NET.Models;

/// <summary>存款利率数据行。</summary>
/// <param name="PubDate">公告日期。</param>
/// <param name="DemandDepositRate">活期存款利率。</param>
/// <param name="FixedDepositRate3Month">定期存款 3 个月利率。</param>
/// <param name="FixedDepositRate6Month">定期存款 6 个月利率。</param>
/// <param name="FixedDepositRate1Year">定期存款 1 年利率。</param>
/// <param name="FixedDepositRate2Year">定期存款 2 年利率。</param>
/// <param name="FixedDepositRate3Year">定期存款 3 年利率。</param>
/// <param name="FixedDepositRate5Year">定期存款 5 年利率。</param>
/// <param name="InstallmentFixedDepositRate1Year">零存整取 1 年利率。</param>
/// <param name="InstallmentFixedDepositRate3Year">零存整取 3 年利率。</param>
/// <param name="InstallmentFixedDepositRate5Year">零存整取 5 年利率。</param>
public sealed record DepositRateRow(
    string? PubDate,
    string? DemandDepositRate,
    string? FixedDepositRate3Month,
    string? FixedDepositRate6Month,
    string? FixedDepositRate1Year,
    string? FixedDepositRate2Year,
    string? FixedDepositRate3Year,
    string? FixedDepositRate5Year,
    string? InstallmentFixedDepositRate1Year,
    string? InstallmentFixedDepositRate3Year,
    string? InstallmentFixedDepositRate5Year);
