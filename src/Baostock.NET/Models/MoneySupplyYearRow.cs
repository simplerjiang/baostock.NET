namespace Baostock.NET.Models;

/// <summary>货币供应量（年底余额）数据行。</summary>
/// <param name="StatYear">统计年份。</param>
/// <param name="M0Year">流通中现金(M0)年度值（亿元）。</param>
/// <param name="M0YearYOY">M0 同比增长率（%）。</param>
/// <param name="M1Year">货币(M1)年度值（亿元）。</param>
/// <param name="M1YearYOY">M1 同比增长率（%）。</param>
/// <param name="M2Year">货币和准货币(M2)年度值（亿元）。</param>
/// <param name="M2YearYOY">M2 同比增长率（%）。</param>
public sealed record MoneySupplyYearRow(
    string? StatYear,
    string? M0Year,
    string? M0YearYOY,
    string? M1Year,
    string? M1YearYOY,
    string? M2Year,
    string? M2YearYOY);
