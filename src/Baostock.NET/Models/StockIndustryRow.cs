namespace Baostock.NET.Models;

/// <summary>行业分类数据行。</summary>
/// <param name="UpdateDate">更新日期。</param>
/// <param name="Code">证券代码。</param>
/// <param name="CodeName">证券名称。</param>
/// <param name="Industry">行业名称。</param>
/// <param name="IndustryClassification">行业分类标准（如“申万行业”）。</param>
public sealed record StockIndustryRow(
    string UpdateDate,
    string Code,
    string CodeName,
    string Industry,
    string IndustryClassification);
