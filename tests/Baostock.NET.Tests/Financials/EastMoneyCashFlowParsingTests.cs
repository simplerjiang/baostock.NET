using Baostock.NET.Financials;
using Baostock.NET.Http;

namespace Baostock.NET.Tests.Financials;

public class EastMoneyCashFlowParsingTests
{
    // v1.4.0 起：fixture 模拟 EM 真实接口（实测 254 字段无 MANANETR，
    // NETCASH_OPERATE 即 CFO，"现金净增加额" 由 CFO+CFI+CFF 派生）。
    // 用茅台 sh.600519 / 2024Q4 实测值：
    //   NETCASH_OPERATE = 92,463,692,168.43  (CFO)
    //   NETCASH_INVEST  = -1,785,202,630.71
    //   NETCASH_FINANCE = -71,067,506,484.81
    //   合计 (净增加额) = 19,610,983,052.91
    private const string SampleJson = @"{
  ""data"": [
    {
      ""SECUCODE"": ""600519.SH"",
      ""REPORT_DATE"": ""2024-12-31 00:00:00"",
      ""REPORT_TYPE"": ""年报"",
      ""SALES_SERVICES"": ""180000000000.00"",
      ""TOTAL_OPERATE_INFLOW"": ""200000000000.00"",
      ""TOTAL_OPERATE_OUTFLOW"": ""107536307831.57"",
      ""NETCASH_OPERATE"": ""92463692168.43"",
      ""TOTAL_INVEST_INFLOW"": ""5000000000.00"",
      ""TOTAL_INVEST_OUTFLOW"": ""6785202630.71"",
      ""NETCASH_INVEST"": ""-1785202630.71"",
      ""TOTAL_FINANCE_INFLOW"": ""1000000000.00"",
      ""TOTAL_FINANCE_OUTFLOW"": ""72067506484.81"",
      ""NETCASH_FINANCE"": ""-71067506484.81"",
      ""BEGIN_CCE"": ""147360188952.47"",
      ""END_CCE"": ""164297949257.83""
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
#pragma warning disable CS0618 // 测试需要继续访问已过时的 NetcashOperate 以验证向后兼容
        // EM 路径无 BREAKING：NetcashOperate 一直是 NETCASH_OPERATE = CFO。
        Assert.Equal(92463692168.43m, first.NetcashOperate);
#pragma warning restore CS0618
        Assert.Equal(-1785202630.71m, first.NetcashInvest);
        Assert.Equal(-71067506484.81m, first.NetcashFinance);
        Assert.Equal(147360188952.47m, first.BeginCce);
        Assert.Equal(164297949257.83m, first.EndCce);
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
#pragma warning disable CS0618
        Assert.Null(second.NetcashOperate);  // "-"
#pragma warning restore CS0618
        Assert.Null(second.NetcashInvest);   // null
        Assert.Equal(170000000000.00m, second.EndCce);
        Assert.Null(second.BeginCce);        // 字段不存在
        Assert.Null(second.TotalOperateInflow);
    }

    [Fact]
    public void Parse_OperatingCashFlow_EqualsNetcashOperate_FromNetcashOperateField()
    {
        // v1.4.0：EM 路径下 OperatingCashFlow / NetcashOperate 都来自 NETCASH_OPERATE
        // （EM 真实接口里这个字段就是 CFO，没有独立的 MANANETR 字段）。
        var rows = EastMoneyCashFlowSource.ParseResponse(SampleJson, "SH600519");
        var first = rows[0];

        Assert.Equal(92463692168.43m, first.OperatingCashFlow);
#pragma warning disable CS0618
        Assert.Equal(first.OperatingCashFlow, first.NetcashOperate);
#pragma warning restore CS0618

        // 第二行 NETCASH_OPERATE = "-" → null
        var second = rows[1];
        Assert.Null(second.OperatingCashFlow);
    }

    [Fact]
    public void Parse_NetCashIncrease_DerivedFromCfoCfiCff()
    {
        // EM 接口本身不返回"现金净增加额"字段（254 字段实测无）；
        // 由 CFO + CFI + CFF 派生。
        var rows = EastMoneyCashFlowSource.ParseResponse(SampleJson, "SH600519");
        var first = rows[0];

        var expected = 92463692168.43m + (-1785202630.71m) + (-71067506484.81m);
        Assert.Equal(expected, first.NetCashIncrease);

        // 第二行三大类不全 → 无法派生 → null
        var second = rows[1];
        Assert.Null(second.NetCashIncrease);
    }

    [Fact]
    public void Parse_MissingDataArray_ThrowsDataSourceException()
    {
        var ex = Assert.Throws<DataSourceException>(() =>
            EastMoneyCashFlowSource.ParseResponse(@"{""rc"":0}", "SH600519"));
        Assert.Equal("EastMoney", ex.SourceName);
    }
}
