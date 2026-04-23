namespace Baostock.NET.Models;

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
