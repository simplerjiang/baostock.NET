namespace Baostock.NET.Models;

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
