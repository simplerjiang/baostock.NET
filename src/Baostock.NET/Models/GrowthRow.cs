namespace Baostock.NET.Models;

/// <summary>季频成长能力数据行。</summary>
/// <param name="Code">证券代码。</param>
/// <param name="PubDate">公告日期。</param>
/// <param name="StatDate">统计截止日期。</param>
/// <param name="YoyEquity">净资产同比增长率（%）。</param>
/// <param name="YoyAsset">总资产同比增长率（%）。</param>
/// <param name="YoyNi">净利润同比增长率（%）。</param>
/// <param name="YoyEpsBasic">基本每股收益同比增长率（%）。</param>
/// <param name="YoyPni">归属母公司股东净利润同比增长率（%）。</param>
public sealed record GrowthRow(
    string Code,
    string? PubDate,
    string? StatDate,
    decimal? YoyEquity,
    decimal? YoyAsset,
    decimal? YoyNi,
    decimal? YoyEpsBasic,
    decimal? YoyPni);
