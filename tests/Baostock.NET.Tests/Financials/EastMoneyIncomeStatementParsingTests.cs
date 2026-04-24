using Baostock.NET.Financials;
using Baostock.NET.Http;

namespace Baostock.NET.Tests.Financials;

public class EastMoneyIncomeStatementParsingTests
{
    private const string SampleJson = @"{
  ""data"": [
    {
      ""SECUCODE"": ""600519.SH"",
      ""REPORT_DATE"": ""2024-12-31 00:00:00"",
      ""REPORT_TYPE"": ""年报"",
      ""TOTAL_OPERATE_INCOME"": ""170000000000.00"",
      ""OPERATE_INCOME"": ""169500000000.00"",
      ""OPERATE_COST"": ""50000000000.00"",
      ""NETPROFIT"": ""85000000000.00"",
      ""PARENT_NETPROFIT"": ""85000000000.00"",
      ""BASIC_EPS"": ""67.69""
    },
    {
      ""SECUCODE"": ""600519.SH"",
      ""REPORT_DATE"": ""2024-09-30 00:00:00"",
      ""REPORT_TYPE"": ""三季报"",
      ""TOTAL_OPERATE_INCOME"": """",
      ""OPERATE_INCOME"": ""-"",
      ""NETPROFIT"": null,
      ""BASIC_EPS"": ""50.12""
    }
  ]
}";

    [Fact]
    public void Parse_SampleJson_ReturnsExpectedRow()
    {
        var rows = EastMoneyIncomeStatementSource.ParseResponse(SampleJson, "sh600519");

        Assert.Equal(2, rows.Count);

        var first = rows[0];
        Assert.Equal("SH600519", first.Code); // 已规范化为东财风格
        Assert.Equal(new DateOnly(2024, 12, 31), first.ReportDate);
        Assert.Equal("年报", first.ReportTitle);
        Assert.Equal(170000000000.00m, first.TotalOperateIncome);
        Assert.Equal(169500000000.00m, first.OperateIncome);
        Assert.Equal(50000000000.00m, first.OperateCost);
        Assert.Equal(85000000000.00m, first.NetProfit);
        Assert.Equal(85000000000.00m, first.ParentNetProfit);
        Assert.Equal(67.69m, first.BasicEps);
        Assert.Equal("EastMoney", first.Source);

        Assert.NotNull(first.RawFields);
        Assert.True(first.RawFields!.ContainsKey("SECUCODE"));
        Assert.True(first.RawFields.ContainsKey("TOTAL_OPERATE_INCOME"));
    }

    [Fact]
    public void Parse_MissingOptionalFields_FieldsAreNull()
    {
        var rows = EastMoneyIncomeStatementSource.ParseResponse(SampleJson, "SH600519");
        var second = rows[1];

        Assert.Equal(new DateOnly(2024, 9, 30), second.ReportDate);
        Assert.Null(second.TotalOperateIncome); // ""
        Assert.Null(second.OperateIncome);       // "-"
        Assert.Null(second.NetProfit);           // null
        Assert.Equal(50.12m, second.BasicEps);
        Assert.Null(second.DilutedEps);          // 字段不存在
        Assert.Null(second.ManageExpense);
    }

    [Fact]
    public void Parse_MissingDataArray_ThrowsDataSourceException()
    {
        var ex = Assert.Throws<DataSourceException>(() =>
            EastMoneyIncomeStatementSource.ParseResponse(@"{""status"":""ok""}", "SH600519"));
        Assert.Equal("EastMoney", ex.SourceName);
    }

    /// <summary>
    /// Finding B-ICBC 回归（EastMoney 分支）：银行模板（companyType=3，如 SH601398 工行）
    /// 实测响应无 <c>TOTAL_OPERATE_INCOME</c>，但 <c>OPERATE_INCOME</c> 有值。业务语义上
    /// 银行的营业总收入 == 营业收入，此处验证在 TotalOperateIncome 缺失时从 OperateIncome 兜底。
    /// </summary>
    [Fact]
    public void Parse_BankTemplate_CopiesOperateIncomeToTotalOperate()
    {
        const string bankJson = @"{
  ""data"": [
    {
      ""SECUCODE"": ""601398.SH"",
      ""REPORT_DATE"": ""2025-12-31 00:00:00"",
      ""REPORT_TYPE"": ""年报"",
      ""OPERATE_INCOME"": ""838270000000"",
      ""INTEREST_NI"": ""635126000000"",
      ""INTEREST_INCOME"": ""1331831000000"",
      ""NETPROFIT"": ""365709000000""
    }
  ]
}";
        var rows = EastMoneyIncomeStatementSource.ParseResponse(bankJson, "SH601398");
        var r = Assert.Single(rows);

        Assert.Equal(838270000000m, r.OperateIncome);
        Assert.Equal(838270000000m, r.TotalOperateIncome); // 从 OperateIncome 兜底
        Assert.Equal(r.OperateIncome, r.TotalOperateIncome);
        Assert.True(r.RawFields!.ContainsKey("INTEREST_NI"));
    }
}
