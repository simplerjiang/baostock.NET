using Baostock.NET.Financials;
using Baostock.NET.Http;

namespace Baostock.NET.Tests.Financials;

public class EastMoneyBalanceSheetParsingTests
{
    // 模拟 zcfzbAjaxNew 响应（2 个报告期；核心字段 + 一个 RawFields 专属字段 EXTRA_FIELD）
    private const string SampleJson = @"{
  ""data"": [
    {
      ""SECUCODE"": ""600519.SH"",
      ""SECURITY_CODE"": ""600519"",
      ""REPORT_DATE"": ""2024-12-31 00:00:00"",
      ""REPORT_TYPE"": ""年报"",
      ""MONEY_CAP"": ""123456789.00"",
      ""ACCOUNTS_RECE"": ""50000000.50"",
      ""TOTAL_ASSETS"": ""999888777666.12"",
      ""TOTAL_LIABILITIES"": ""100000000000.00"",
      ""TOTAL_EQUITY"": ""899888777666.12"",
      ""EXTRA_FIELD"": ""preserved-in-raw""
    },
    {
      ""SECUCODE"": ""600519.SH"",
      ""SECURITY_CODE"": ""600519"",
      ""REPORT_DATE"": ""2024-09-30 00:00:00"",
      ""REPORT_TYPE"": ""三季报"",
      ""MONEY_CAP"": ""98765432.00"",
      ""ACCOUNTS_RECE"": """",
      ""TOTAL_ASSETS"": ""-"",
      ""TOTAL_LIABILITIES"": null,
      ""TOTAL_EQUITY"": ""700000000000.00""
    }
  ]
}";

    [Fact]
    public void Parse_SampleJson_ReturnsExpectedRow()
    {
        var rows = EastMoneyBalanceSheetSource.ParseResponse(SampleJson, "SH600519");

        Assert.Equal(2, rows.Count);

        var first = rows[0]; // 按输入顺序：2024-12-31
        Assert.Equal("SH600519", first.Code);
        Assert.Equal(new DateOnly(2024, 12, 31), first.ReportDate);
        Assert.Equal("年报", first.ReportTitle);
        Assert.Equal(123456789.00m, first.MoneyCap);
        Assert.Equal(50000000.50m, first.AccountsRece);
        Assert.Equal(999888777666.12m, first.TotalAssets);
        Assert.Equal(100000000000.00m, first.TotalLiabilities);
        Assert.Equal(899888777666.12m, first.TotalEquity);
        Assert.Equal("EastMoney", first.Source);

        // RawFields 保留所有原始字段
        Assert.NotNull(first.RawFields);
        Assert.True(first.RawFields!.ContainsKey("EXTRA_FIELD"));
        Assert.Equal("preserved-in-raw", first.RawFields["EXTRA_FIELD"]);
        Assert.True(first.RawFields.ContainsKey("SECUCODE"));
        Assert.True(first.RawFields.ContainsKey("REPORT_DATE"));
    }

    [Fact]
    public void Parse_MissingOptionalFields_FieldsAreNull()
    {
        var rows = EastMoneyBalanceSheetSource.ParseResponse(SampleJson, "SH600519");
        var second = rows[1]; // 2024-09-30：含 空串 / "-" / null

        Assert.Equal(new DateOnly(2024, 9, 30), second.ReportDate);
        Assert.Equal("三季报", second.ReportTitle);
        Assert.Equal(98765432.00m, second.MoneyCap);
        Assert.Null(second.AccountsRece);          // 空字符串 → null
        Assert.Null(second.TotalAssets);           // "-" → null
        Assert.Null(second.TotalLiabilities);      // JSON null → null
        Assert.Equal(700000000000.00m, second.TotalEquity);

        // 未出现在响应中的其它字段也应为 null
        Assert.Null(second.Inventory);
        Assert.Null(second.FixedAsset);
        Assert.Null(second.ShareCapital);
    }

    [Fact]
    public void Parse_MissingDataArray_ThrowsDataSourceException()
    {
        var json = @"{""code"":0}";
        var ex = Assert.Throws<DataSourceException>(() =>
            EastMoneyBalanceSheetSource.ParseResponse(json, "SH600519"));
        Assert.Equal("EastMoney", ex.SourceName);
    }
}
