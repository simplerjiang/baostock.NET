namespace Baostock.NET.Models;

/// <summary>交易日数据行。</summary>
/// <param name="Date">日期。</param>
/// <param name="IsTrading">当天是否为交易日。</param>
public sealed record TradeDateRow(DateOnly Date, bool IsTrading);
