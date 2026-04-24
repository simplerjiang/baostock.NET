namespace Baostock.NET.Models;

/// <summary>
/// 东方财富 / 腾讯历史 K 线行（v1.2.0 Sprint 2 Phase 2）。
/// </summary>
/// <remarks>
/// <para>本模型与 baostock TCP 的 <see cref="KLineRow"/> 解耦，原因：</para>
/// <list type="bullet">
///   <item><description>EM/Tencent 不返回 PreClose / TradeStatus / IsST，复用 <see cref="KLineRow"/> 会出现大量 null；</description></item>
///   <item><description>复权语义对齐东财（fqt=1 前复权 = <see cref="AdjustFlag.PreAdjust"/>），与 baostock <c>adjustflag=2</c> 数值不同；</description></item>
///   <item><description>命名空间一致性：实时行情已采用独立 <c>RealtimeQuote</c>，K 线沿用相同风格。</description></item>
/// </list>
/// <para><b>量纲</b>：<c>Volume</c>=股（EM/Tencent 原始为「手」由 SDK ×100 归一化）；
/// <c>Amount</c>=元；百分比字段（振幅、涨跌幅、换手率）为原值（如 <c>1.55</c> 表示 1.55%）。
/// 腾讯不返回 Amount/Amplitude/ChangePercent/ChangeAmount/TurnoverRate，置 <see langword="null"/>。</para>
/// </remarks>
/// <param name="Code">东方财富风格代码，如 <c>SH600519</c>。</param>
/// <param name="Date">K 线时间戳；日/周/月线为当天 0 点（<see cref="System.DateTimeKind.Unspecified"/>），分钟线为该 K 线右端时刻（北京时间，<see cref="System.DateTimeKind.Unspecified"/>）。</param>
/// <param name="Open">开盘价。</param>
/// <param name="Close">收盘价。</param>
/// <param name="High">最高价。</param>
/// <param name="Low">最低价。</param>
/// <param name="Volume">成交量（股）。</param>
/// <param name="Amount">成交额（元）。腾讯不提供时为 <see langword="null"/>。</param>
/// <param name="Amplitude">振幅（%）。腾讯不提供时为 <see langword="null"/>。</param>
/// <param name="ChangePercent">涨跌幅（%）。腾讯不提供时为 <see langword="null"/>。</param>
/// <param name="ChangeAmount">涨跌额（元）。腾讯不提供时为 <see langword="null"/>。</param>
/// <param name="TurnoverRate">换手率（%）。腾讯不提供时为 <see langword="null"/>。</param>
/// <param name="Source">数据来源："EastMoney" / "Tencent"。</param>
public sealed record EastMoneyKLineRow(
    string Code,
    System.DateTime Date,
    decimal Open,
    decimal Close,
    decimal High,
    decimal Low,
    long Volume,
    decimal? Amount,
    decimal? Amplitude,
    decimal? ChangePercent,
    decimal? ChangeAmount,
    decimal? TurnoverRate,
    string Source);
