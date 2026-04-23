namespace Baostock.NET.Models;

/// <summary>季频杜邦指数数据行。</summary>
/// <param name="Code">证券代码。</param>
/// <param name="PubDate">公告日期。</param>
/// <param name="StatDate">统计截止日期。</param>
/// <param name="DupontRoe">净资产收益率。</param>
/// <param name="DupontAssetStoEquity">权益乘数（杠杆比率）。</param>
/// <param name="DupontAssetTurn">总资产周转率。</param>
/// <param name="DupontPnitoni">归属母公司股东的净利润/净利润。</param>
/// <param name="DupontNitogr">净利润/营业总收入。</param>
/// <param name="DupontTaxBurden">税负比率（净利润/利润总额）。</param>
/// <param name="DupontIntburden">利息负担比率（利润总额/息税前利润）。</param>
/// <param name="DupontEbittogr">息税前利润/营业总收入。</param>
public sealed record DupontRow(
    string Code,
    string? PubDate,
    string? StatDate,
    decimal? DupontRoe,
    decimal? DupontAssetStoEquity,
    decimal? DupontAssetTurn,
    decimal? DupontPnitoni,
    decimal? DupontNitogr,
    decimal? DupontTaxBurden,
    decimal? DupontIntburden,
    decimal? DupontEbittogr);
