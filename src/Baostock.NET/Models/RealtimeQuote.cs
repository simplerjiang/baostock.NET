namespace Baostock.NET.Models;

/// <summary>
/// 多源对冲后的实时行情快照。字段对齐三源公共能力，量纲统一。
/// </summary>
/// <remarks>
/// 量纲规约（v1.2.0 Sprint 2）：
/// <list type="bullet">
///   <item><description><see cref="Volume"/> 统一到 <b>股</b>。腾讯 / 东财原始单位为「手」，对应解析时 ×100；新浪原始即「股」。</description></item>
///   <item><description><see cref="Amount"/> 统一到 <b>元</b>，三源原始单位即元。</description></item>
///   <item><description>价格保留原始精度（<see cref="decimal"/>），不做四舍五入。东财原始为整数 × 10^-<c>f152</c>，解析时按 <c>f152</c> 反归一化。</description></item>
///   <item><description><see cref="Timestamp"/> 为北京时间，<c>DateTimeKind = Unspecified</c>（与 baostock TCP 协议惯例保持一致）。</description></item>
/// </list>
/// 字段映射依据见 <c>docs/v1.2.0-source-mapping.md</c> §2 表 1。
/// </remarks>
/// <param name="Code">东方财富风格代码，如 <c>SH600519</c> / <c>SZ000001</c> / <c>BJ430047</c>。</param>
/// <param name="Name">股票名称（中文）。</param>
/// <param name="Open">今日开盘价（元）。</param>
/// <param name="PreClose">昨日收盘价（元）。</param>
/// <param name="Last">当前价 / 最新成交价（元）。</param>
/// <param name="High">今日最高价（元）。</param>
/// <param name="Low">今日最低价（元）。</param>
/// <param name="Bid1">买一价（元）；无该档数据时为 <see langword="null"/>。</param>
/// <param name="Ask1">卖一价（元）；无该档数据时为 <see langword="null"/>。</param>
/// <param name="Volume">成交量（<b>股</b>）。</param>
/// <param name="Amount">成交额（元）。</param>
/// <param name="Timestamp">行情时间（北京时间，<c>Kind = Unspecified</c>）。</param>
/// <param name="Source">数据源名称：<c>"Tencent"</c> / <c>"Sina"</c> / <c>"EastMoney"</c>。</param>
public sealed record RealtimeQuote(
    string Code,
    string Name,
    decimal Open,
    decimal PreClose,
    decimal Last,
    decimal High,
    decimal Low,
    decimal? Bid1,
    decimal? Ask1,
    long Volume,
    decimal Amount,
    DateTime Timestamp,
    string Source);
