namespace Baostock.NET.Models;

/// <summary>特殊股票（终止上市/暂停上市/ST/*ST）数据行。</summary>
/// <param name="UpdateDate">更新日期。</param>
/// <param name="Code">证券代码。</param>
/// <param name="CodeName">证券名称。</param>
public sealed record SpecialStockRow(string UpdateDate, string Code, string CodeName);
