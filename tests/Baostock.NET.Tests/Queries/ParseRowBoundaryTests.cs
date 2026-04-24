using Baostock.NET.Client;
using Baostock.NET.Models;

namespace Baostock.NET.Tests.Queries;

public class ParseRowBoundaryTests
{
    // === ParseKLineRow 测试 ===

    [Fact]
    public void ParseKLineRow_FullColumns_ParsesAllFields()
    {
        // 14 列完整数据
        var cols = new[] { "2024-01-02", "sh.600000", "10.50", "10.80", "10.30", "10.60", "10.45", "1000000", "10500000.00", "3", "1.23", "1", "0.50", "0" };
        var row = BaostockClient.ParseKLineRow(cols);

        Assert.Equal(new DateOnly(2024, 1, 2), row.Date);
        Assert.Equal("SH600000", row.Code);
        Assert.Equal(10.50m, row.Open);
        Assert.Equal(10.80m, row.High);
        Assert.Equal(10.30m, row.Low);
        Assert.Equal(10.60m, row.Close);
        Assert.Equal(10.45m, row.PreClose);
        Assert.Equal(1000000L, row.Volume);
        Assert.Equal(10500000.00m, row.Amount);
        Assert.Equal(AdjustFlag.NoAdjust, row.AdjustFlag);
        Assert.Equal(1.23m, row.Turn);
        Assert.Equal(TradeStatus.Normal, row.TradeStatus);
        Assert.Equal(0.50m, row.PctChg);
        Assert.False(row.IsST);
    }

    [Fact]
    public void ParseKLineRow_OnlyDateColumn_DoesNotThrow()
    {
        // 只有 1 列（date）
        var cols = new[] { "2024-01-02" };
        var row = BaostockClient.ParseKLineRow(cols);

        Assert.Equal(new DateOnly(2024, 1, 2), row.Date);
        Assert.Equal(string.Empty, row.Code);
        Assert.Null(row.Open);
        Assert.Null(row.Close);
        Assert.Null(row.Volume);
        Assert.Equal(AdjustFlag.NoAdjust, row.AdjustFlag);
        Assert.Equal(TradeStatus.Suspended, row.TradeStatus);
        Assert.Null(row.PctChg);
        Assert.False(row.IsST);
    }

    [Fact]
    public void ParseKLineRow_TenColumns_FillsRemainingWithDefaults()
    {
        // 10 列 — 少了 turn, tradestatus, pctChg, isST
        var cols = new[] { "2024-01-02", "sh.600000", "10.50", "10.80", "10.30", "10.60", "10.45", "1000000", "10500000.00", "1" };
        var row = BaostockClient.ParseKLineRow(cols);

        Assert.Equal(new DateOnly(2024, 1, 2), row.Date);
        Assert.Equal("SH600000", row.Code);
        Assert.Equal(10.60m, row.Close);
        Assert.Equal(AdjustFlag.PostAdjust, row.AdjustFlag);
        Assert.Null(row.Turn);
        Assert.Equal(TradeStatus.Suspended, row.TradeStatus);
        Assert.Null(row.PctChg);
        Assert.False(row.IsST);
    }

    [Fact]
    public void ParseKLineRow_EmptyFields_ReturnsNulls()
    {
        // 14 列但部分字段为空
        var cols = new[] { "2024-01-02", "sh.600000", "", "", "", "", "", "", "", "", "", "", "", "" };
        var row = BaostockClient.ParseKLineRow(cols);

        Assert.Equal(new DateOnly(2024, 1, 2), row.Date);
        Assert.Null(row.Open);
        Assert.Null(row.High);
        Assert.Null(row.Low);
        Assert.Null(row.Close);
        Assert.Null(row.Volume);
        Assert.Equal(AdjustFlag.NoAdjust, row.AdjustFlag);
        Assert.Equal(TradeStatus.Suspended, row.TradeStatus);
    }

    [Fact]
    public void ParseKLineRow_IsST_True()
    {
        var cols = new[] { "2024-01-02", "sh.600000", "10.50", "10.80", "10.30", "10.60", "10.45", "1000000", "10500000.00", "1", "1.23", "1", "0.50", "1" };
        var row = BaostockClient.ParseKLineRow(cols);
        Assert.True(row.IsST);
    }

    // === ParseMinuteKLineRow 测试 ===

    [Fact]
    public void ParseMinuteKLineRow_FullColumns_ParsesAllFields()
    {
        // 10 列完整数据
        var cols = new[] { "2024-01-02", "14:30:00", "sh.600000", "10.50", "10.80", "10.30", "10.60", "500000", "5250000.00", "1" };
        var row = BaostockClient.ParseMinuteKLineRow(cols);

        Assert.Equal(new DateOnly(2024, 1, 2), row.Date);
        Assert.Equal("14:30:00", row.Time);
        Assert.Equal("SH600000", row.Code);
        Assert.Equal(10.50m, row.Open);
        Assert.Equal(10.80m, row.High);
        Assert.Equal(10.30m, row.Low);
        Assert.Equal(10.60m, row.Close);
        Assert.Equal(500000L, row.Volume);
        Assert.Equal(5250000.00m, row.Amount);
        Assert.Equal(AdjustFlag.PostAdjust, row.AdjustFlag);
    }

    [Fact]
    public void ParseMinuteKLineRow_OnlyDateColumn_DoesNotThrow()
    {
        var cols = new[] { "2024-01-02" };
        var row = BaostockClient.ParseMinuteKLineRow(cols);

        Assert.Equal(new DateOnly(2024, 1, 2), row.Date);
        Assert.Equal(string.Empty, row.Time);
        Assert.Equal(string.Empty, row.Code);
        Assert.Null(row.Open);
        Assert.Null(row.Close);
        Assert.Null(row.Volume);
        Assert.Equal(AdjustFlag.NoAdjust, row.AdjustFlag);
    }

    [Fact]
    public void ParseMinuteKLineRow_FiveColumns_FillsRemainingWithDefaults()
    {
        // 5 列 — 少了 low, close, volume, amount, adjustflag
        var cols = new[] { "2024-01-02", "09:30:00", "sh.600000", "10.50", "10.80" };
        var row = BaostockClient.ParseMinuteKLineRow(cols);

        Assert.Equal("09:30:00", row.Time);
        Assert.Equal(10.50m, row.Open);
        Assert.Equal(10.80m, row.High);
        Assert.Null(row.Low);
        Assert.Null(row.Close);
        Assert.Null(row.Volume);
        Assert.Equal(AdjustFlag.NoAdjust, row.AdjustFlag);
    }

    // === Code 字段反向格式化降级行为 ===

    [Fact]
    public void ParseKLineRow_UnknownCodeFormat_FallsBackToOriginal()
    {
        // 服务器极少数情况下可能返回 CodeFormatter 无法识别的代码（指数/ETF 特殊代码等），
        // 解析层应保留原始字符串，不抛异常。
        var cols = new[] { "2024-01-02", "unknownexchange.xxxxxx", "10.50", "10.80", "10.30", "10.60", "10.45", "1000000", "10500000.00", "1", "1.23", "1", "0.50", "0" };
        var row = BaostockClient.ParseKLineRow(cols);

        Assert.Equal("unknownexchange.xxxxxx", row.Code);
        Assert.Equal(10.50m, row.Open);
    }

    [Fact]
    public void ParseMinuteKLineRow_UnknownCodeFormat_FallsBackToOriginal()
    {
        var cols = new[] { "2024-01-02", "09:30:00", "weirdformat", "10.50", "10.80", "10.30", "10.60", "500000", "5250000.00", "1" };
        var row = BaostockClient.ParseMinuteKLineRow(cols);

        Assert.Equal("weirdformat", row.Code);
    }
}
