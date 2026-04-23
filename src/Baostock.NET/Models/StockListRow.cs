namespace Baostock.NET.Models;

/// <summary>证券列表数据行。</summary>
/// <param name="Code">证券代码，如 <c>sh.600000</c>。</param>
/// <param name="TradeStatus">交易状态。</param>
/// <param name="CodeName">证券名称。</param>
public sealed record StockListRow(string Code, string TradeStatus, string CodeName);
