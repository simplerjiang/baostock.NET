namespace Baostock.NET.Models;

/// <summary>证券基本资料数据行。</summary>
/// <param name="Code">证券代码。</param>
/// <param name="CodeName">证券名称。</param>
/// <param name="IpoDate">上市日期。</param>
/// <param name="OutDate">退市日期。</param>
/// <param name="Type">证券类型（1=股票，2=指数，3=其它，4=可转债，5=ETF）。</param>
/// <param name="Status">上市状态（1=上市，0=退市）。</param>
public sealed record StockBasicRow(
    string Code,
    string CodeName,
    string? IpoDate,
    string? OutDate,
    string Type,
    string Status);
