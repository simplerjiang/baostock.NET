using Baostock.NET.Util;

namespace Baostock.NET.Tests.Util;

public class CodeFormatterTests
{
    // ---------- Parse: 全部 6 种输入格式 × SH/SZ/BJ ----------

    [Theory]
    // 东财大写
    [InlineData("SH600519", Exchange.ShangHai, "600519")]
    [InlineData("SZ000001", Exchange.ShenZhen, "000001")]
    [InlineData("BJ430047", Exchange.BeiJing, "430047")]
    // 小写无点
    [InlineData("sh600519", Exchange.ShangHai, "600519")]
    [InlineData("sz000001", Exchange.ShenZhen, "000001")]
    [InlineData("bj430047", Exchange.BeiJing, "430047")]
    // baostock 风格
    [InlineData("sh.600519", Exchange.ShangHai, "600519")]
    [InlineData("sz.000001", Exchange.ShenZhen, "000001")]
    [InlineData("bj.430047", Exchange.BeiJing, "430047")]
    // 反向风格
    [InlineData("600519.SH", Exchange.ShangHai, "600519")]
    [InlineData("000001.SZ", Exchange.ShenZhen, "000001")]
    [InlineData("430047.BJ", Exchange.BeiJing, "430047")]
    // secid SH / SZ
    [InlineData("1.600519", Exchange.ShangHai, "600519")]
    [InlineData("0.000001", Exchange.ShenZhen, "000001")]
    // 仅 6 位（按数字段推断）
    [InlineData("600519", Exchange.ShangHai, "600519")]
    [InlineData("000001", Exchange.ShenZhen, "000001")]
    [InlineData("430047", Exchange.BeiJing, "430047")]
    public void Parse_ValidInputs_ReturnsExpected(string input, Exchange expectedExchange, string expectedCode)
    {
        var sc = CodeFormatter.Parse(input);
        Assert.Equal(expectedExchange, sc.Exchange);
        Assert.Equal(expectedCode, sc.Code6);
    }

    // ---------- 反向输出对：每个 Exchange 各一组 ----------

    [Fact]
    public void EastMoneyForm_AllExchanges()
    {
        Assert.Equal("SH600519", CodeFormatter.Parse("sh.600519").EastMoneyForm);
        Assert.Equal("SZ000001", CodeFormatter.Parse("000001.SZ").EastMoneyForm);
        Assert.Equal("BJ430047", CodeFormatter.Parse("bj430047").EastMoneyForm);
    }

    [Fact]
    public void BaostockForm_AllExchanges()
    {
        Assert.Equal("sh.600519", CodeFormatter.Parse("SH600519").BaostockForm);
        Assert.Equal("sz.000001", CodeFormatter.Parse("SZ000001").BaostockForm);
        Assert.Equal("bj.430047", CodeFormatter.Parse("BJ430047").BaostockForm);
    }

    [Fact]
    public void LowercaseNoDot_AllExchanges()
    {
        Assert.Equal("sh600519", CodeFormatter.Parse("SH600519").LowercaseNoDot);
        Assert.Equal("sz000001", CodeFormatter.Parse("SZ000001").LowercaseNoDot);
        Assert.Equal("bj430047", CodeFormatter.Parse("BJ430047").LowercaseNoDot);
    }

    [Theory]
    [InlineData("SH600519", "1.600519")]
    [InlineData("SZ000001", "0.000001")]
    public void SecId_SH_SZ(string input, string expected)
    {
        Assert.Equal(expected, CodeFormatter.Parse(input).SecId);
    }

    [Fact(Skip = "BJ secid prefix not validated")]
    public void SecId_BJ_PrefixToBeValidated()
    {
        // 占位：Sprint 2 完成线上采样后再补断言（究竟是 0.{code} 还是 1.{code}）。
        Assert.Equal("0.430047", CodeFormatter.Parse("BJ430047").SecId);
    }

    // ---------- CninfoStock ----------

    [Theory]
    [InlineData("SH600519", "600519,gssh0600519")]
    [InlineData("SZ000001", "000001,gssz0000001")]
    [InlineData("BJ430047", "430047,gssb0430047")]
    public void CninfoStock_AllExchanges(string input, string expected)
    {
        Assert.Equal(expected, CodeFormatter.Parse(input).CninfoStock);
        Assert.Equal(expected, CodeFormatter.ToCninfoStock(input));
    }

    // ---------- 指数代码：显式前缀完全被尊重（不再因数字段推断冲突而抛异常） ----------

    [Theory]
    [InlineData("SH000001", Exchange.ShangHai, "000001")]   // 上证综指
    [InlineData("SH000300", Exchange.ShangHai, "000300")]   // 沪深300（SH 侧）
    [InlineData("SH000016", Exchange.ShangHai, "000016")]   // 上证50
    [InlineData("SZ399001", Exchange.ShenZhen, "399001")]   // 深证成指
    [InlineData("SZ399006", Exchange.ShenZhen, "399006")]   // 创业板指
    [InlineData("sh.000001", Exchange.ShangHai, "000001")]  // baostock 风格
    [InlineData("000001.SH", Exchange.ShangHai, "000001")]  // 后缀风格
    public void Parse_IndexCodes_RespectsExplicitPrefix(string input, Exchange expectedExchange, string expectedCode)
    {
        var sc = CodeFormatter.Parse(input);
        Assert.Equal(expectedExchange, sc.Exchange);
        Assert.Equal(expectedCode, sc.Code6);
    }

    // ---------- 6 位非数字 ----------

    [Theory]
    [InlineData("SH60051A")]
    [InlineData("sh.60051A")]
    [InlineData("60051A.SH")]
    [InlineData("1.60051A")]
    public void Parse_NonDigitCode_ThrowsFormatException(string input)
    {
        Assert.Throws<FormatException>(() => CodeFormatter.Parse(input));
    }

    // ---------- 空字符串、null、过短、过长输入 ----------

    [Fact]
    public void Parse_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CodeFormatter.Parse(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("SH")]
    [InlineData("SH60")]
    [InlineData("SH6005199")]      // 过长
    [InlineData("SH600519XYZ")]    // 过长
    [InlineData("XX600519")]       // 未知前缀
    [InlineData("700000")]         // 未知数字段（7 开头）
    [InlineData("2.600519")]       // 未知 secid 前缀
    public void Parse_InvalidInputs_ThrowsFormatException(string input)
    {
        Assert.Throws<FormatException>(() => CodeFormatter.Parse(input));
    }

    [Fact]
    public void Parse_BJSecId_NotSupported_ThrowsFormatException()
    {
        // BJ 北交所代码段（如 430047/830xxx）通过 secid 反解时应明确拒绝
        Assert.Throws<FormatException>(() => CodeFormatter.Parse("0.430047"));
    }

    // ---------- TryParse ----------

    [Fact]
    public void TryParse_Success_ReturnsTrueAndCode()
    {
        Assert.True(CodeFormatter.TryParse("SH600519", out var sc));
        Assert.Equal(Exchange.ShangHai, sc.Exchange);
        Assert.Equal("600519", sc.Code6);
    }

    [Fact]
    public void TryParse_Failure_ReturnsFalse()
    {
        Assert.False(CodeFormatter.TryParse("XX600519", out _));
        Assert.False(CodeFormatter.TryParse(null!, out _));
        Assert.False(CodeFormatter.TryParse("bad", out _));
    }

    // ---------- 便捷 To* 方法 ----------

    [Fact]
    public void ConvenienceConverters_RoundTrip()
    {
        Assert.Equal("SH600519", CodeFormatter.ToEastMoney("sh.600519"));
        Assert.Equal("sh.600519", CodeFormatter.ToBaostock("SH600519"));
        Assert.Equal("1.600519", CodeFormatter.ToSecId("SH600519"));
        Assert.Equal("600519,gssh0600519", CodeFormatter.ToCninfoStock("SH600519"));
    }

    // ---------- 便捷 To* 方法非法入参：抛 ArgumentException（含 InnerException=FormatException） ----------

    [Theory]
    [InlineData("XX600519")]
    [InlineData("700000")]
    [InlineData("bad")]
    [InlineData("")]
    public void Convenience_InvalidInput_ToBaostock_ThrowsArgumentException(string input)
    {
        var ex = Assert.Throws<ArgumentException>(() => CodeFormatter.ToBaostock(input));
        Assert.Equal("anyForm", ex.ParamName);
        Assert.IsType<FormatException>(ex.InnerException);
    }

    [Theory]
    [InlineData("XX600519")]
    [InlineData("700000")]
    [InlineData("bad")]
    public void Convenience_InvalidInput_ToEastMoney_ThrowsArgumentException(string input)
    {
        var ex = Assert.Throws<ArgumentException>(() => CodeFormatter.ToEastMoney(input));
        Assert.Equal("anyForm", ex.ParamName);
        Assert.IsType<FormatException>(ex.InnerException);
    }

    [Theory]
    [InlineData("XX600519")]
    [InlineData("BJ430047")] // BJ secid 不支持反解，但 ToSecId 自身是 SH/SZ 路径；这里用真正非法输入
    public void Convenience_InvalidInput_ToSecId_ThrowsArgumentException(string input)
    {
        // 注：BJ430047 自身是有效解析，会成功生成 secid（按当前实现 0.{code}），故仅断言非法前缀场景
        if (input == "BJ430047")
        {
            // 合法输入，不应抛
            var s = CodeFormatter.ToSecId(input);
            Assert.False(string.IsNullOrEmpty(s));
            return;
        }
        var ex = Assert.Throws<ArgumentException>(() => CodeFormatter.ToSecId(input));
        Assert.Equal("anyForm", ex.ParamName);
        Assert.IsType<FormatException>(ex.InnerException);
    }

    [Theory]
    [InlineData("XX600519")]
    [InlineData("700000")]
    public void Convenience_InvalidInput_ToCninfoStock_ThrowsArgumentException(string input)
    {
        var ex = Assert.Throws<ArgumentException>(() => CodeFormatter.ToCninfoStock(input));
        Assert.Equal("anyForm", ex.ParamName);
        Assert.IsType<FormatException>(ex.InnerException);
    }

    [Fact]
    public void Convenience_NullInput_ThrowsArgumentNullException()
    {
        // null 路径仍抛 ArgumentNullException（来自 Parse 内部检查）
        Assert.Throws<ArgumentNullException>(() => CodeFormatter.ToBaostock(null!));
        Assert.Throws<ArgumentNullException>(() => CodeFormatter.ToEastMoney(null!));
        Assert.Throws<ArgumentNullException>(() => CodeFormatter.ToSecId(null!));
        Assert.Throws<ArgumentNullException>(() => CodeFormatter.ToCninfoStock(null!));
    }
}
