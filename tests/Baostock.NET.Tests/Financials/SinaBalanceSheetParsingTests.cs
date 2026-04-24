using Baostock.NET.Financials;
using Baostock.NET.Http;

namespace Baostock.NET.Tests.Financials;

public class SinaBalanceSheetParsingTests
{
    // 模拟新浪 getFinanceReport2022 (source=fzb) 响应：
    // - 2024-12-31 含主要字段 + 同比率 + 一个未建模字段（留到 RawFields）
    // - 2024-09-30 仅 CURFDS、INVE=空字符串（应 → null）、TOTASSET="-"
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
          ""is_audit"": ""是"",
          ""publish_date"": ""2025-04-01"",
          ""rCurrency"": ""CNY"",
          ""rType"": ""合并期末"",
          ""data"": [
            {""item_title"": ""货币资金"", ""item_field"": ""CURFDS"", ""item_value"": ""123456789.00"", ""item_tongbi"": ""0.12""},
            {""item_title"": ""应收账款"", ""item_field"": ""ACCRVABL"", ""item_value"": ""50000000.50""},
            {""item_title"": ""存货"", ""item_field"": ""INVE"", ""item_value"": ""30000000""},
            {""item_title"": ""资产总计"", ""item_field"": ""TOTASSET"", ""item_value"": ""999888777666.12""},
            {""item_title"": ""负债合计"", ""item_field"": ""TOTLIAB"", ""item_value"": ""100000000000.00""},
            {""item_title"": ""股东权益合计"", ""item_field"": ""TOTSHAREHLDREQT"", ""item_value"": ""899888777666.12""},
            {""item_title"": ""备注字段"", ""item_field"": ""EXTRA_FIELD"", ""item_value"": ""preserved-in-raw""}
          ]
        },
        ""2024-09-30"": {
          ""rType"": ""合并期末"",
          ""data"": [
            {""item_title"": ""货币资金"", ""item_field"": ""CURFDS"", ""item_value"": ""98765432.00""},
            {""item_title"": ""存货"", ""item_field"": ""INVE"", ""item_value"": """"},
            {""item_title"": ""资产总计"", ""item_field"": ""TOTASSET"", ""item_value"": ""-""}
          ]
        }
      }
    }
  }
}";

    [Fact]
    public void Parse_SampleJson_ReturnsExpectedRow()
    {
        var rows = SinaBalanceSheetSource.ParseResponse(SampleJson, "SH600519");

        Assert.Equal(2, rows.Count);

        var first = rows.Single(r => r.ReportDate == new DateOnly(2024, 12, 31));
        Assert.Equal("SH600519", first.Code);
        Assert.Equal("合并期末", first.ReportTitle);
        Assert.Equal(123456789.00m, first.MoneyCap);
        Assert.Equal(50000000.50m, first.AccountsRece);
        Assert.Equal(30000000m, first.Inventory);
        Assert.Equal(999888777666.12m, first.TotalAssets);
        Assert.Equal(100000000000.00m, first.TotalLiabilities);
        Assert.Equal(899888777666.12m, first.TotalEquity);
        Assert.Equal("Sina", first.Source);

        Assert.NotNull(first.RawFields);
        Assert.Equal("preserved-in-raw", first.RawFields!["EXTRA_FIELD"]);
        Assert.Equal("0.12", first.RawFields["CURFDS_TONGBI"]);
        Assert.Equal("123456789.00", first.RawFields["CURFDS"]);
        Assert.Equal("是", first.RawFields["is_audit"]);
        Assert.Equal("2025-04-01", first.RawFields["publish_date"]);
    }

    [Fact]
    public void Parse_MissingOptionalFields_FieldsAreNull()
    {
        var rows = SinaBalanceSheetSource.ParseResponse(SampleJson, "SH600519");
        var second = rows.Single(r => r.ReportDate == new DateOnly(2024, 9, 30));

        Assert.Equal(98765432.00m, second.MoneyCap);
        Assert.Null(second.Inventory);     // 空字符串 → null
        Assert.Null(second.TotalAssets);   // "-" → null
        // 未出现的字段均为 null
        Assert.Null(second.AccountsRece);
        Assert.Null(second.TotalLiabilities);
        Assert.Null(second.TotalEquity);
        Assert.Null(second.FixedAsset);
    }

    [Fact]
    public void Parse_MissingResult_ThrowsDataSourceException()
    {
        var json = @"{""foo"": 1}";
        var ex = Assert.Throws<DataSourceException>(() =>
            SinaBalanceSheetSource.ParseResponse(json, "SH600519"));
        Assert.Equal("Sina", ex.SourceName);
    }
}
