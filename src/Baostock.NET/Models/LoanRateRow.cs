namespace Baostock.NET.Models;

public sealed record LoanRateRow(
    string? PubDate,
    string? LoanRate6Month,
    string? LoanRate6MonthTo1Year,
    string? LoanRate1YearTo3Year,
    string? LoanRate3YearTo5Year,
    string? LoanRateAbove5Year,
    string? MortgateRateBelow5Year,
    string? MortgateRateAbove5Year);
