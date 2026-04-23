namespace Baostock.NET.Models;

/// <summary>存款准备金率数据行。</summary>
/// <param name="PubDate">公告日期。</param>
/// <param name="EffectiveDate">生效日期。</param>
/// <param name="BigInstitutionsRatioPre">大型机构调整前准备金率（%）。</param>
/// <param name="BigInstitutionsRatioAfter">大型机构调整后准备金率（%）。</param>
/// <param name="MediumInstitutionsRatioPre">中小型机构调整前准备金率（%）。</param>
/// <param name="MediumInstitutionsRatioAfter">中小型机构调整后准备金率（%）。</param>
public sealed record ReserveRatioRow(
    string? PubDate,
    string? EffectiveDate,
    string? BigInstitutionsRatioPre,
    string? BigInstitutionsRatioAfter,
    string? MediumInstitutionsRatioPre,
    string? MediumInstitutionsRatioAfter);
