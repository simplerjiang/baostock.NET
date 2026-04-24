using Baostock.NET.Financials;
using Baostock.NET.Http;

namespace Baostock.NET.Tests.Financials;

public class SinaIncomeStatementParsingTests
{
    private const string SampleJson = @"{
  ""result"": {
    ""status"": {""code"": 0, ""msg"": """"},
    ""data"": {
      ""report_date"": [
        {""date_value"": ""2024-12-31""},
        {""date_value"": ""2024-09-30""}
      ],
      ""report_list"": {
        ""2024-12-31"": {
          ""rType"": ""合并期末"",
          ""data"": [
            {""item_title"": ""营业总收入"", ""item_field"": ""TOTAREVE"", ""item_value"": ""170000000000.00"", ""item_tongbi"": ""0.15""},
            {""item_title"": ""营业收入"", ""item_field"": ""OPERREV"", ""item_value"": ""169500000000.00""},
            {""item_title"": ""营业成本"", ""item_field"": ""OPERCOST"", ""item_value"": ""50000000000.00""},
            {""item_title"": ""净利润"", ""item_field"": ""NETPROFIT"", ""item_value"": ""85000000000.00""},
            {""item_title"": ""归母净利润"", ""item_field"": ""PARENETP"", ""item_value"": ""85000000000.00""},
            {""item_title"": ""基本每股收益"", ""item_field"": ""BASICEPS"", ""item_value"": ""67.69""},
            {""item_title"": ""备注字段"", ""item_field"": ""EXTRA_FIELD"", ""item_value"": ""preserved-in-raw""}
          ]
        },
        ""2024-09-30"": {
          ""rType"": ""合并期末"",
          ""data"": [
            {""item_title"": ""营业总收入"", ""item_field"": ""TOTAREVE"", ""item_value"": ""120000000000.00""},
            {""item_title"": ""营业成本"", ""item_field"": ""OPERCOST"", ""item_value"": """"},
            {""item_title"": ""净利润"", ""item_field"": ""NETPROFIT"", ""item_value"": ""--""}
          ]
        }
      }
    }
  }
}";

    [Fact]
    public void Parse_SampleJson_ReturnsExpectedRow()
    {
        var rows = SinaIncomeStatementSource.ParseResponse(SampleJson, "SH600519");

        Assert.Equal(2, rows.Count);
        var first = rows.Single(r => r.ReportDate == new DateOnly(2024, 12, 31));

        Assert.Equal("SH600519", first.Code);
        Assert.Equal("合并期末", first.ReportTitle);
        Assert.Equal(170000000000.00m, first.TotalOperateIncome);
        Assert.Equal(169500000000.00m, first.OperateIncome);
        Assert.Equal(50000000000.00m, first.OperateCost);
        Assert.Equal(85000000000.00m, first.NetProfit);
        Assert.Equal(85000000000.00m, first.ParentNetProfit);
        Assert.Equal(67.69m, first.BasicEps);
        Assert.Equal("Sina", first.Source);

        Assert.NotNull(first.RawFields);
        Assert.Equal("preserved-in-raw", first.RawFields!["EXTRA_FIELD"]);
        Assert.Equal("0.15", first.RawFields["TOTAREVE_TONGBI"]);
    }

    [Fact]
    public void Parse_MissingOptionalFields_FieldsAreNull()
    {
        var rows = SinaIncomeStatementSource.ParseResponse(SampleJson, "SH600519");
        var second = rows.Single(r => r.ReportDate == new DateOnly(2024, 9, 30));

        Assert.Equal(120000000000.00m, second.TotalOperateIncome);
        Assert.Null(second.OperateCost);   // 空字符串 → null
        Assert.Null(second.NetProfit);     // "--" → null
        Assert.Null(second.OperateIncome);
        Assert.Null(second.ParentNetProfit);
        Assert.Null(second.BasicEps);
    }

    [Fact]
    public void Parse_MissingResult_ThrowsDataSourceException()
    {
        var json = @"{""result"": {""status"": {""code"": 0}}}"; // 缺 result.data
        var ex = Assert.Throws<DataSourceException>(() =>
            SinaIncomeStatementSource.ParseResponse(json, "SH600519"));
        Assert.Equal("Sina", ex.SourceName);
    }

    /// <summary>
    /// N-01 回归：新浪 2026 线上利润表实际字段名改为 <c>BIZTOTINCO</c> / <c>BIZINCO</c> / <c>BIZTOTCOST</c> /
    /// <c>BIZCOST</c> / <c>SALESEXPE</c> / <c>FINEXPE</c> / <c>PERPROFIT</c> / <c>TOTPROFIT</c>，
    /// 本用例保证新字段别名被正确映射到 <see cref="Baostock.NET.Models.FullIncomeStatementRow"/> 顶层字段上。
    /// </summary>
    [Fact]
    public void Parse_LiveSinaFieldAliases_MappedToTopLevel()
    {
        const string liveShapeJson = @"{
  ""result"": {
    ""status"": {""code"": 0, ""msg"": """"},
    ""data"": {
      ""report_list"": {
        ""20241231"": {
          ""rType"": ""合并期末"",
          ""data"": [
            {""item_field"": ""BIZTOTINCO"",   ""item_value"": ""458502407000.000000""},
            {""item_field"": ""BIZINCO"",      ""item_value"": ""456451731000.000000""},
            {""item_field"": ""BIZTOTCOST"",   ""item_value"": ""409076613000.000000""},
            {""item_field"": ""BIZCOST"",      ""item_value"": ""335989528000.000000""},
            {""item_field"": ""SALESEXPE"",    ""item_value"": ""42891490000.000000""},
            {""item_field"": ""MANAEXPE"",     ""item_value"": ""16092311000.000000""},
            {""item_field"": ""DEVEEXPE"",     ""item_value"": ""17787624000.000000""},
            {""item_field"": ""FINEXPE"",      ""item_value"": ""-5903546000.000000""},
            {""item_field"": ""PERPROFIT"",    ""item_value"": ""52978773000.000000""},
            {""item_field"": ""TOTPROFIT"",    ""item_value"": ""53085343000.000000""},
            {""item_field"": ""INCOTAXEXPE"",  ""item_value"": ""8565147000.000000""},
            {""item_field"": ""NETPROFIT"",    ""item_value"": ""44520196000.000000""},
            {""item_field"": ""PARENETP"",     ""item_value"": ""43945411000.000000""},
            {""item_field"": ""BASICEPS"",     ""item_value"": ""5.800000""},
            {""item_field"": ""DILUTEDEPS"",   ""item_value"": ""5.760000""}
          ]
        }
      }
    }
  }
}";
        var rows = SinaIncomeStatementSource.ParseResponse(liveShapeJson, "SZ000333");
        var r = Assert.Single(rows);

        Assert.Equal(new DateOnly(2024, 12, 31), r.ReportDate);
        Assert.Equal(458502407000m, r.TotalOperateIncome);
        Assert.Equal(456451731000m, r.OperateIncome);
        Assert.Equal(409076613000m, r.TotalOperateCost);
        Assert.Equal(335989528000m, r.OperateCost);
        Assert.Equal(42891490000m, r.SaleExpense);
        Assert.Equal(16092311000m, r.ManageExpense);
        Assert.Equal(17787624000m, r.ResearchExpense);
        Assert.Equal(-5903546000m, r.FinanceExpense);
        Assert.Equal(52978773000m, r.OperateProfit);
        Assert.Equal(53085343000m, r.TotalProfit);
        Assert.Equal(8565147000m, r.IncomeTax);
        Assert.Equal(44520196000m, r.NetProfit);
        Assert.Equal(43945411000m, r.ParentNetProfit);
        Assert.Equal(5.80m, r.BasicEps);
        Assert.Equal(5.76m, r.DilutedEps);
    }

    /// <summary>
    /// Finding B-ICBC 回归：银行/券商利润表模板上游不返回 <c>BIZTOTINCO</c>（营业总收入），
    /// 但返回 <c>BIZINCO</c>（营业收入）和 <c>INTEINCO</c>（利息收入，银行标识字段）。
    /// 银行语义上 “营业总收入 == 营业收入”，此处验证在 TotalOperateIncome 缺失时
    /// 从 OperateIncome 兜底复制。
    /// </summary>
    [Fact]
    public void Parse_BankTemplate_CopiesOperateIncomeToTotalOperate()
    {
        const string bankJson = @"{
  ""result"": {
    ""status"": {""code"": 0, ""msg"": """"},
    ""data"": {
      ""report_list"": {
        ""20251231"": {
          ""rType"": ""合并期末"",
          ""data"": [
            {""item_field"": ""BIZINCO"",      ""item_value"": ""838270000000.000000""},
            {""item_field"": ""INTEINCO"",     ""item_value"": ""1331831000000.000000""},
            {""item_field"": ""NETINTEINCO"",  ""item_value"": ""635126000000.000000""},
            {""item_field"": ""NETPROFIT"",    ""item_value"": ""365709000000.000000""}
          ]
        }
      }
    }
  }
}";
        var rows = SinaIncomeStatementSource.ParseResponse(bankJson, "SH601398");
        var r = Assert.Single(rows);

        Assert.Equal(838270000000m, r.OperateIncome);
        Assert.Equal(838270000000m, r.TotalOperateIncome); // 从 OperateIncome 兜底
        Assert.Equal(r.OperateIncome, r.TotalOperateIncome);
        // 兜底不应修改 RawFields（保留上游原始字段集，无 BIZTOTINCO）
        Assert.False(r.RawFields!.ContainsKey("BIZTOTINCO"));
        Assert.True(r.RawFields.ContainsKey("INTEINCO"));
    }
}
