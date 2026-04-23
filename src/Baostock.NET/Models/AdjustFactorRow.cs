namespace Baostock.NET.Models;

/// <summary>复权因子数据行。</summary>
/// <param name="Code">证券代码。</param>
/// <param name="DividOperateDate">除权除息日。</param>
/// <param name="ForeAdjustFactor">前复权因子。</param>
/// <param name="BackAdjustFactor">后复权因子。</param>
/// <param name="AdjustFactor">复权因子。</param>
public sealed record AdjustFactorRow(
    string Code,
    string? DividOperateDate,
    decimal? ForeAdjustFactor,
    decimal? BackAdjustFactor,
    decimal? AdjustFactor);
