namespace Baostock.NET.Models;

public sealed record StockIndustryRow(
    string UpdateDate,
    string Code,
    string CodeName,
    string Industry,
    string IndustryClassification);
