namespace Baostock.NET.Util;

/// <summary>
/// 东方财富 <c>secid</c> 前缀解析工具：<c>1.{code}</c>(SH) / <c>0.{code}</c>(SZ) / <c>116.{code}</c>(BJ)。
/// </summary>
/// <remarks>
/// <para>v1.2.0 Sprint 2 Phase 2 抽出共享方法，避免实时行情、K 线两处重复维护 BJ 前缀 TODO。</para>
/// <para><b>北交所 secid</b>：当前按社区惯例使用 <c>116.{code}</c> 前缀，Sprint 0 未线上采样验证；
/// 集成测试通过 hedge fallback 兼容（若 EM 失败可回退到 Tencent）。</para>
/// </remarks>
public static class EastMoneySecIdResolver
{
    /// <summary>把任意支持的代码格式解析为东财 secid。</summary>
    /// <param name="anyForm">任意支持的格式（<c>SH600519</c> / <c>sh600519</c> / <c>sh.600519</c> 等）。</param>
    /// <returns>东财 secid 字符串（如 <c>1.600519</c>、<c>0.000001</c>、<c>116.430047</c>）。</returns>
    /// <exception cref="System.FormatException">输入不是任何已知格式。</exception>
    public static string Resolve(string anyForm)
    {
        var sc = CodeFormatter.Parse(anyForm);
        return Resolve(sc);
    }

    /// <summary>把已解析的 <see cref="StockCode"/> 转换为东财 secid。</summary>
    /// <param name="code">已解析的标准化代码。</param>
    /// <returns>东财 secid 字符串。</returns>
    /// <exception cref="System.InvalidOperationException">未知 <see cref="Exchange"/>。</exception>
    public static string Resolve(StockCode code) => code.Exchange switch
    {
        Exchange.ShangHai => "1." + code.Code6,
        Exchange.ShenZhen => "0." + code.Code6,
        // TODO(Sprint 3+)：BJ 北交所 secid 前缀 116 来自社区惯例，Sprint 0 未线上采样；
        // hedge 调度会在 EM 失败时 fallback 到 Tencent。Sprint 3 拉到稳定 BJ K 线 fixture 后再固化。
        Exchange.BeiJing => "116." + code.Code6,
        _ => throw new System.InvalidOperationException("未知 Exchange"),
    };
}
