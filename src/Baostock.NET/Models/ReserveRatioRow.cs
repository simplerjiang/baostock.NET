namespace Baostock.NET.Models;

public sealed record ReserveRatioRow(
    string? PubDate,
    string? EffectiveDate,
    string? BigInstitutionsRatioPre,
    string? BigInstitutionsRatioAfter,
    string? MediumInstitutionsRatioPre,
    string? MediumInstitutionsRatioAfter);
