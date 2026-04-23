namespace Baostock.NET.Models;

/// <summary>指数成分股数据行。</summary>
/// <param name="UpdateDate">更新日期。</param>
/// <param name="Code">证券代码。</param>
/// <param name="CodeName">证券名称。</param>
public sealed record IndexConstituentRow(string UpdateDate, string Code, string CodeName);
