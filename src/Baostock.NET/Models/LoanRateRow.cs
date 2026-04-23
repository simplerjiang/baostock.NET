namespace Baostock.NET.Models;

/// <summary>贷款利率数据行。</summary>
/// <param name="PubDate">公告日期。</param>
/// <param name="LoanRate6Month">6 个月以内贷款利率。</param>
/// <param name="LoanRate6MonthTo1Year">6 个月至 1 年贷款利率。</param>
/// <param name="LoanRate1YearTo3Year">1 年至 3 年贷款利率。</param>
/// <param name="LoanRate3YearTo5Year">3 年至 5 年贷款利率。</param>
/// <param name="LoanRateAbove5Year">5 年以上贷款利率。</param>
/// <param name="MortgateRateBelow5Year">个人住房公积金贷款 5 年以下利率。</param>
/// <param name="MortgateRateAbove5Year">个人住房公积金贷款 5 年以上利率。</param>
public sealed record LoanRateRow(
    string? PubDate,
    string? LoanRate6Month,
    string? LoanRate6MonthTo1Year,
    string? LoanRate1YearTo3Year,
    string? LoanRate3YearTo5Year,
    string? LoanRateAbove5Year,
    string? MortgateRateBelow5Year,
    string? MortgateRateAbove5Year);
