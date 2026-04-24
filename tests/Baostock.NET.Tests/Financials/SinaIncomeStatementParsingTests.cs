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
}
