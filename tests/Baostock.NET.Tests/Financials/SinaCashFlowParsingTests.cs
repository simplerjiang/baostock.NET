using Baostock.NET.Financials;
using Baostock.NET.Http;

namespace Baostock.NET.Tests.Financials;

public class SinaCashFlowParsingTests
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
            {""item_title"": ""销售商品提供劳务收到的现金"", ""item_field"": ""SALESSERVICE"", ""item_value"": ""180000000000.00""},
            {""item_title"": ""经营活动现金流入小计"", ""item_field"": ""SUBTOTCASHINFL"", ""item_value"": ""200000000000.00""},
            {""item_title"": ""经营活动现金流出小计"", ""item_field"": ""SUBTOTCASHOUTF"", ""item_value"": ""80000000000.00""},
            {""item_title"": ""经营活动现金流量净额"", ""item_field"": ""NETCFOPER"", ""item_value"": ""120000000000.00"", ""item_tongbi"": ""0.25""},
            {""item_title"": ""投资活动现金流量净额"", ""item_field"": ""NETCFINVE"", ""item_value"": ""-30000000000.00""},
            {""item_title"": ""筹资活动现金流量净额"", ""item_field"": ""NETCFFIN"", ""item_value"": ""-50000000000.00""},
            {""item_title"": ""期初现金及现金等价物余额"", ""item_field"": ""BEGPERIOCASH"", ""item_value"": ""40000000000.00""},
            {""item_title"": ""期末现金及现金等价物余额"", ""item_field"": ""ENDPERIOCASH"", ""item_value"": ""80000000000.00""},
            {""item_title"": ""备注字段"", ""item_field"": ""EXTRA_FIELD"", ""item_value"": ""preserved-in-raw""}
          ]
        },
        ""2024-09-30"": {
          ""rType"": ""合并期末"",
          ""data"": [
            {""item_title"": ""销售商品提供劳务收到的现金"", ""item_field"": ""SALESSERVICE"", ""item_value"": ""140000000000.00""},
            {""item_title"": ""经营活动现金流量净额"", ""item_field"": ""NETCFOPER"", ""item_value"": """"},
            {""item_title"": ""期末现金及现金等价物余额"", ""item_field"": ""ENDPERIOCASH"", ""item_value"": ""-""}
          ]
        }
      }
    }
  }
}";

    [Fact]
    public void Parse_SampleJson_ReturnsExpectedRow()
    {
        var rows = SinaCashFlowSource.ParseResponse(SampleJson, "SH600519");

        Assert.Equal(2, rows.Count);
        var first = rows.Single(r => r.ReportDate == new DateOnly(2024, 12, 31));

        Assert.Equal("SH600519", first.Code);
        Assert.Equal("合并期末", first.ReportTitle);
        Assert.Equal(180000000000.00m, first.SalesServices);
        Assert.Equal(200000000000.00m, first.TotalOperateInflow);
        Assert.Equal(80000000000.00m, first.TotalOperateOutflow);
        Assert.Equal(120000000000.00m, first.NetcashOperate);
        Assert.Equal(-30000000000.00m, first.NetcashInvest);
        Assert.Equal(-50000000000.00m, first.NetcashFinance);
        Assert.Equal(40000000000.00m, first.BeginCce);
        Assert.Equal(80000000000.00m, first.EndCce);
        Assert.Equal("Sina", first.Source);

        Assert.NotNull(first.RawFields);
        Assert.Equal("preserved-in-raw", first.RawFields!["EXTRA_FIELD"]);
        Assert.Equal("0.25", first.RawFields["NETCFOPER_TONGBI"]);
    }

    [Fact]
    public void Parse_MissingOptionalFields_FieldsAreNull()
    {
        var rows = SinaCashFlowSource.ParseResponse(SampleJson, "SH600519");
        var second = rows.Single(r => r.ReportDate == new DateOnly(2024, 9, 30));

        Assert.Equal(140000000000.00m, second.SalesServices);
        Assert.Null(second.NetcashOperate);  // 空字符串 → null
        Assert.Null(second.EndCce);          // "-" → null
        Assert.Null(second.TotalOperateInflow);
        Assert.Null(second.NetcashInvest);
        Assert.Null(second.NetcashFinance);
        Assert.Null(second.BeginCce);
    }

    [Fact]
    public void Parse_MissingResult_ThrowsDataSourceException()
    {
        var json = @"{""result"": {""data"": {""report_date"": []}}}"; // 缺 result.data.report_list
        var ex = Assert.Throws<DataSourceException>(() =>
            SinaCashFlowSource.ParseResponse(json, "SH600519"));
        Assert.Equal("Sina", ex.SourceName);
    }
}
