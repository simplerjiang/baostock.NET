namespace Baostock.NET.Models;

/// <summary>货币供应量（月度）数据行。</summary>
/// <param name="StatYear">统计年份。</param>
/// <param name="StatMonth">统计月份。</param>
/// <param name="M0Month">流通中现金(M0)月度值（亿元）。</param>
/// <param name="M0YOY">M0 同比增长率（%）。</param>
/// <param name="M0ChainRelative">M0 环比增长率（%）。</param>
/// <param name="M1Month">货币(M1)月度值（亿元）。</param>
/// <param name="M1YOY">M1 同比增长率（%）。</param>
/// <param name="M1ChainRelative">M1 环比增长率（%）。</param>
/// <param name="M2Month">货币和准货币(M2)月度值（亿元）。</param>
/// <param name="M2YOY">M2 同比增长率（%）。</param>
/// <param name="M2ChainRelative">M2 环比增长率（%）。</param>
public sealed record MoneySupplyMonthRow(
    string? StatYear,
    string? StatMonth,
    string? M0Month,
    string? M0YOY,
    string? M0ChainRelative,
    string? M1Month,
    string? M1YOY,
    string? M1ChainRelative,
    string? M2Month,
    string? M2YOY,
    string? M2ChainRelative);
