using System.Globalization;

namespace Baostock.NET.Util;

/// <summary>A 股交易所枚举。</summary>
public enum Exchange
{
    /// <summary>上海证券交易所（SH，6 开头）。</summary>
    ShangHai,
    /// <summary>深圳证券交易所（SZ，0 / 3 开头）。</summary>
    ShenZhen,
    /// <summary>北京证券交易所（BJ，4 / 8 / 92 开头）。</summary>
    BeiJing,
}

/// <summary>
/// 标准化股票代码：交易所 + 6 位数字代码。
/// 通过各 <c>*Form</c> 属性可一次性获取常见接口风格字符串。
/// </summary>
/// <param name="Exchange">交易所。</param>
/// <param name="Code6">6 位数字代码（构造时已校验）。</param>
public readonly record struct StockCode(Exchange Exchange, string Code6)
{
    /// <summary>东方财富风格（SDK 对外主格式）：<c>SH600519</c> / <c>SZ000001</c> / <c>BJ430047</c>。</summary>
    public string EastMoneyForm => UpperPrefix + Code6;

    /// <summary>baostock TCP 协议风格：<c>sh.600519</c> / <c>sz.000001</c> / <c>bj.430047</c>。</summary>
    public string BaostockForm => LowerPrefix + "." + Code6;

    /// <summary>腾讯 / 新浪小写无点风格：<c>sh600519</c> / <c>sz000001</c> / <c>bj430047</c>。</summary>
    public string LowercaseNoDot => LowerPrefix + Code6;

    /// <summary>
    /// 东方财富 secid 风格：<c>1.{code}</c>(SH) / <c>0.{code}</c>(SZ)。
    /// TODO(Sprint 2)：BJ 北交所 secid 前缀尚未线上采样验证，当前临时按 <c>0.{code}</c> 输出。
    /// </summary>
    public string SecId => SecIdPrefix + "." + Code6;

    /// <summary>巨潮资讯网 orgId 风格：<c>gss{h|z|b}0{6位代码}</c>，如 <c>gssh0600519</c>。</summary>
    /// <remarks>TODO(Sprint 2)：BJ 北交所 orgId 实际形式（<c>gssb</c>）尚需巨潮线上验证。</remarks>
    public string CninfoOrgId => "gss" + ShortLower + "0" + Code6;

    /// <summary>巨潮资讯网公告查询入参风格：<c>{6位代码},{CninfoOrgId}</c>，如 <c>600519,gssh0600519</c>。</summary>
    public string CninfoStock => Code6 + "," + CninfoOrgId;

    private string UpperPrefix => Exchange switch
    {
        Exchange.ShangHai => "SH",
        Exchange.ShenZhen => "SZ",
        Exchange.BeiJing => "BJ",
        _ => throw new InvalidOperationException("未知 Exchange"),
    };

    private string LowerPrefix => Exchange switch
    {
        Exchange.ShangHai => "sh",
        Exchange.ShenZhen => "sz",
        Exchange.BeiJing => "bj",
        _ => throw new InvalidOperationException("未知 Exchange"),
    };

    private string ShortLower => Exchange switch
    {
        Exchange.ShangHai => "h",
        Exchange.ShenZhen => "z",
        Exchange.BeiJing => "b",
        _ => throw new InvalidOperationException("未知 Exchange"),
    };

    private string SecIdPrefix => Exchange == Exchange.ShangHai ? "1" : "0";
}

/// <summary>
/// 股票代码格式互转工具。所有 <c>To*</c> 便捷方法都先 <see cref="Parse(string)"/>，再生成目标格式字符串。
/// 解析失败统一抛出 <see cref="FormatException"/>；输入为 <c>null</c> 抛 <see cref="ArgumentNullException"/>。
/// </summary>
public static class CodeFormatter
{
    /// <summary>
    /// 把任意支持的格式解析为 <see cref="StockCode"/>。
    /// </summary>
    /// <remarks>
    /// 支持输入：
    /// <list type="bullet">
    ///   <item><description><c>SH600519</c> / <c>SZ000001</c> / <c>BJ430047</c>（东财大写，SDK 主格式）</description></item>
    ///   <item><description><c>sh600519</c> / <c>sz000001</c> / <c>bj430047</c>（小写无点）</description></item>
    ///   <item><description><c>sh.600519</c> / <c>sz.000001</c> / <c>bj.430047</c>（baostock 风格）</description></item>
    ///   <item><description><c>600519.SH</c> / <c>000001.SZ</c> / <c>430047.BJ</c>（部分接口风格）</description></item>
    ///   <item><description><c>1.600519</c> / <c>0.000001</c>（东财 secid，<b>BJ 不能从 secid 反解</b>）</description></item>
    /// </list>
    /// 输入显式带前缀（SH/SZ/BJ）时<b>完全尊重显式前缀</b>，不再按 6 位数字段推断校验，
    /// 以兼容指数代码（如 <c>SH000001</c> 上证综指、<c>SZ399001</c> 深证成指等）。
    /// 仅当输入为纯 6 位数字（无前缀）或 secid 风格 <c>0.{code}</c> 时才按数字段推断交易所。
    /// </remarks>
    /// <param name="anyForm">任意支持的格式字符串。</param>
    /// <returns>标准化后的 <see cref="StockCode"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="anyForm"/> 为 <c>null</c>。</exception>
    /// <exception cref="FormatException">输入不符合任何已知格式。</exception>
    public static StockCode Parse(string anyForm)
    {
        if (anyForm is null)
        {
            throw new ArgumentNullException(nameof(anyForm));
        }

        if (!TryParseCore(anyForm, out var code, out var error))
        {
            throw new FormatException(error);
        }
        return code;
    }

    /// <summary>解析失败返回 <see langword="false"/>，不抛异常。</summary>
    /// <param name="anyForm">任意支持的格式字符串。</param>
    /// <param name="code">解析成功时填充结果。</param>
    /// <returns>是否解析成功。</returns>
    public static bool TryParse(string anyForm, out StockCode code)
    {
        if (anyForm is null)
        {
            code = default;
            return false;
        }
        return TryParseCore(anyForm, out code, out _);
    }

    /// <summary>转换为东方财富风格（SDK 对外主格式），如 <c>SH600519</c>。</summary>
    /// <param name="anyForm">任意支持的格式。</param>
    /// <returns>东方财富风格字符串。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="anyForm"/> 为 <c>null</c>。</exception>
    /// <exception cref="ArgumentException">输入不符合任何已知格式（<c>InnerException</c> 为 <see cref="FormatException"/>）。</exception>
    public static string ToEastMoney(string anyForm)
    {
        try { return Parse(anyForm).EastMoneyForm; }
        catch (FormatException ex) { throw new ArgumentException($"Invalid stock code format: '{anyForm}'", nameof(anyForm), ex); }
    }

    /// <summary>转换为 baostock TCP 风格，如 <c>sh.600519</c>。</summary>
    /// <param name="anyForm">任意支持的格式。</param>
    /// <returns>baostock 风格字符串。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="anyForm"/> 为 <c>null</c>。</exception>
    /// <exception cref="ArgumentException">输入不符合任何已知格式（<c>InnerException</c> 为 <see cref="FormatException"/>）。</exception>
    public static string ToBaostock(string anyForm)
    {
        try { return Parse(anyForm).BaostockForm; }
        catch (FormatException ex) { throw new ArgumentException($"Invalid stock code format: '{anyForm}'", nameof(anyForm), ex); }
    }

    /// <summary>转换为东方财富 secid 风格，如 <c>1.600519</c>。</summary>
    /// <param name="anyForm">任意支持的格式。</param>
    /// <returns>secid 风格字符串。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="anyForm"/> 为 <c>null</c>。</exception>
    /// <exception cref="ArgumentException">输入不符合任何已知格式（<c>InnerException</c> 为 <see cref="FormatException"/>）。</exception>
    public static string ToSecId(string anyForm)
    {
        try { return Parse(anyForm).SecId; }
        catch (FormatException ex) { throw new ArgumentException($"Invalid stock code format: '{anyForm}'", nameof(anyForm), ex); }
    }

    /// <summary>转换为巨潮 stock 入参，如 <c>600519,gssh0600519</c>。</summary>
    /// <param name="anyForm">任意支持的格式。</param>
    /// <returns>巨潮风格字符串。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="anyForm"/> 为 <c>null</c>。</exception>
    /// <exception cref="ArgumentException">输入不符合任何已知格式（<c>InnerException</c> 为 <see cref="FormatException"/>）。</exception>
    public static string ToCninfoStock(string anyForm)
    {
        try { return Parse(anyForm).CninfoStock; }
        catch (FormatException ex) { throw new ArgumentException($"Invalid stock code format: '{anyForm}'", nameof(anyForm), ex); }
    }

    private static bool TryParseCore(string raw, out StockCode code, out string error)
    {
        code = default;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "股票代码不能为空。";
            return false;
        }

        var s = raw.Trim();

        // 1) baostock 风格：sh.600519 / sz.000001 / bj.430047
        if (s.Length == 9 && s[2] == '.')
        {
            var prefix = s.Substring(0, 2);
            var num = s.Substring(3);
            return TryFinish(prefix, num, requireExplicit: true, out code, out error);
        }

        // 2) 反向风格：600519.SH / 000001.SZ / 430047.BJ
        if (s.Length == 9 && s[6] == '.')
        {
            var num = s.Substring(0, 6);
            var prefix = s.Substring(7);
            return TryFinish(prefix, num, requireExplicit: true, out code, out error);
        }

        // 3) secid 风格：1.600519(SH) / 0.000001 或 0.{code}(SZ)；BJ 不可反解
        if (s.Length == 8 && s[1] == '.' && (s[0] == '0' || s[0] == '1'))
        {
            var num = s.Substring(2);
            if (!IsSixDigits(num))
            {
                error = $"secid 风格代码段必须是 6 位数字：'{raw}'。";
                return false;
            }
            if (s[0] == '1')
            {
                // 1.{code} 一律视为 SH
                return TryFinish("SH", num, requireExplicit: true, out code, out error);
            }
            // 0.{code}：可能是 SZ 也可能是 BJ；secid 自身无法区分 BJ，故按数字段推断
            var inferred = InferByCode(num);
            if (inferred is null)
            {
                error = $"secid 风格无法识别交易所，且代码段无法从数字推断：'{raw}'。";
                return false;
            }
            if (inferred == Exchange.BeiJing)
            {
                error = $"secid 风格不支持反解 BJ 北交所代码（前缀尚未验证）：'{raw}'。";
                return false;
            }
            code = new StockCode(inferred.Value, num);
            return true;
        }

        // 4) 东财 / 小写无点风格：SH600519 / sh600519 / SZ000001 / sz000001 / BJ430047 / bj430047
        if (s.Length == 8)
        {
            var prefix = s.Substring(0, 2);
            var num = s.Substring(2);
            return TryFinish(prefix, num, requireExplicit: true, out code, out error);
        }

        // 5) 仅 6 位代码（无前缀）—— 按数字段推断
        if (s.Length == 6 && IsSixDigits(s))
        {
            var inferred = InferByCode(s);
            if (inferred is null)
            {
                error = $"6 位代码无法推断交易所：'{raw}'。";
                return false;
            }
            code = new StockCode(inferred.Value, s);
            return true;
        }

        error = $"无法识别的股票代码格式：'{raw}'。";
        return false;
    }

    private static bool TryFinish(string prefix, string num, bool requireExplicit, out StockCode code, out string error)
    {
        _ = requireExplicit; // 当前所有带前缀分支都视为显式
        code = default;
        error = string.Empty;

        if (!IsSixDigits(num))
        {
            error = $"代码段必须是 6 位数字：'{num}'。";
            return false;
        }

        if (!TryParsePrefix(prefix, out var explicitEx))
        {
            error = $"未知交易所前缀：'{prefix}'。";
            return false;
        }

        // 显式前缀完全尊重，不再校验是否与数字段推断一致（兼容指数代码如 SH000001 / SZ399001）
        code = new StockCode(explicitEx, num);
        return true;
    }

    private static bool TryParsePrefix(string prefix, out Exchange exchange)
    {
        switch (prefix.ToUpperInvariant())
        {
            case "SH": exchange = Exchange.ShangHai; return true;
            case "SZ": exchange = Exchange.ShenZhen; return true;
            case "BJ": exchange = Exchange.BeiJing; return true;
            default: exchange = default; return false;
        }
    }

    private static Exchange? InferByCode(string code6)
    {
        var c0 = code6[0];
        if (c0 == '6')
        {
            return Exchange.ShangHai;
        }
        if (c0 == '0' || c0 == '3')
        {
            return Exchange.ShenZhen;
        }
        if (c0 == '4' || c0 == '8')
        {
            return Exchange.BeiJing;
        }
        if (code6.StartsWith("92", StringComparison.Ordinal))
        {
            return Exchange.BeiJing;
        }
        return null;
    }

    private static bool IsSixDigits(string s)
    {
        if (s.Length != 6)
        {
            return false;
        }
        for (var i = 0; i < 6; i++)
        {
            if (s[i] < '0' || s[i] > '9')
            {
                return false;
            }
        }
        // 强制十进制即可（防御一些奇怪 Unicode 数字）
        return int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }
}
