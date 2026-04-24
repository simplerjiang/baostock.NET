using Baostock.NET.Financials;
using Baostock.NET.Http;

namespace Baostock.NET.Tests.Financials;

public class EastMoneyCashFlowParsingTests
{
    private const string SampleJson = @"{
  ""data"": [
    {
      ""SECUCODE"": ""600519.SH"",
      ""REPORT_DATE"": ""2024-12-31 00:00:00"",
      ""REPORT_TYPE"": ""年报"",
      ""SALES_SERVICES"": ""180000000000.00"",
      ""NETCASH_OPERATE"": ""90000000000.00"",
      ""NETCASH_INVEST"": ""-20000000000.00"",
      ""NETCASH_FINANCE"": ""-30000000000.00"",
      ""BEGIN_CCE"": ""150000000000.00"",
      ""END_CCE"": ""190000000000.00""
    },
    {
      ""SECUCODE"": ""600519.SH"",
      ""REPORT_DATE"": ""2024-09-30 00:00:00"",
      ""REPORT_TYPE"": ""三季报"",
      ""SALES_SERVICES"": """",
      ""NETCASH_OPERATE"": ""-"",
      ""NETCASH_INVEST"": null,
      ""END_CCE"": ""170000000000.00""
    }
  ]
}";

    [Fact]
    public void Parse_SampleJson_ReturnsExpectedRow()
    {
        var rows = EastMoneyCashFlowSource.ParseResponse(SampleJson, "SH600519");

        Assert.Equal(2, rows.Count);

        var first = rows[0];
        Assert.Equal("SH600519", first.Code);
        Assert.Equal(new DateOnly(2024, 12, 31), first.ReportDate);
        Assert.Equal("年报", first.ReportTitle);
        Assert.Equal(180000000000.00m, first.SalesServices);
        Assert.Equal(90000000000.00m, first.NetcashOperate);
        Assert.Equal(-20000000000.00m, first.NetcashInvest);
        Assert.Equal(-30000000000.00m, first.NetcashFinance);
        Assert.Equal(150000000000.00m, first.BeginCce);
        Assert.Equal(190000000000.00m, first.EndCce);
        Assert.Equal("EastMoney", first.Source);

        Assert.NotNull(first.RawFields);
        Assert.True(first.RawFields!.ContainsKey("SALES_SERVICES"));
        Assert.True(first.RawFields.ContainsKey("SECUCODE"));
    }

    [Fact]
    public void Parse_MissingOptionalFields_FieldsAreNull()
    {
        var rows = EastMoneyCashFlowSource.ParseResponse(SampleJson, "SH600519");
        var second = rows[1];

        Assert.Equal(new DateOnly(2024, 9, 30), second.ReportDate);
        Assert.Null(second.SalesServices);   // ""
        Assert.Null(second.NetcashOperate);  // "-"
        Assert.Null(second.NetcashInvest);   // null
        Assert.Equal(170000000000.00m, second.EndCce);
        Assert.Null(second.BeginCce);        // 字段不存在
        Assert.Null(second.TotalOperateInflow);
    }

    [Fact]
    public void Parse_MissingDataArray_ThrowsDataSourceException()
    {
        var ex = Assert.Throws<DataSourceException>(() =>
            EastMoneyCashFlowSource.ParseResponse(@"{""rc"":0}", "SH600519"));
        Assert.Equal("EastMoney", ex.SourceName);
    }
}
