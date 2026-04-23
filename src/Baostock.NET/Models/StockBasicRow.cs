namespace Baostock.NET.Models;

public sealed record StockBasicRow(
    string Code,
    string CodeName,
    string? IpoDate,
    string? OutDate,
    string Type,
    string Status);
