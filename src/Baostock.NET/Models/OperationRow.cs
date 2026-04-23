namespace Baostock.NET.Models;

/// <summary>季频营运能力数据行。</summary>
/// <param name="Code">证券代码。</param>
/// <param name="PubDate">公告日期。</param>
/// <param name="StatDate">统计截止日期。</param>
/// <param name="NrTurnRatio">应收账款周转率。</param>
/// <param name="NrTurnDays">应收账款周转天数。</param>
/// <param name="InvTurnRatio">存货周转率。</param>
/// <param name="InvTurnDays">存货周转天数。</param>
/// <param name="CaTurnRatio">流动资产周转率。</param>
/// <param name="AssetTurnRatio">总资产周转率。</param>
public sealed record OperationRow(
    string Code,
    string? PubDate,
    string? StatDate,
    decimal? NrTurnRatio,
    decimal? NrTurnDays,
    decimal? InvTurnRatio,
    decimal? InvTurnDays,
    decimal? CaTurnRatio,
    decimal? AssetTurnRatio);
