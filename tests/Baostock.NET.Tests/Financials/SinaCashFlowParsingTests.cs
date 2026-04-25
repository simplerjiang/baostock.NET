using Baostock.NET.Financials;
using Baostock.NET.Http;

namespace Baostock.NET.Tests.Financials;

public class SinaCashFlowParsingTests
{
    // v1.4.0 起：fixture 模拟 Sina 真实 llb 接口，必须同时包含
    // MANANETR（经营活动现金流量净额, CFO）与 CASHNETR（现金及现金等价物净增加额）
    // 以验证两者被分别映射到 OperatingCashFlow / NetCashIncrease。
    // 茅台 sh.600519 / 2024Q4 实测对照：
    //   MANANETR = 92,463,692,168.43
    //   CASHNETR = 19,609,900,305.36
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
            {""item_title"": ""经营活动现金流出小计"", ""item_field"": ""SUBTOTCASHOUTF"", ""item_value"": ""107536307831.57""},
            {""item_title"": ""经营活动现金流量净额"", ""item_field"": ""MANANETR"", ""item_value"": ""92463692168.43"", ""item_tongbi"": ""0.25""},
            {""item_title"": ""投资活动现金流量净额"", ""item_field"": ""NETCASHINVE"", ""item_value"": ""-1785202630.71""},
            {""item_title"": ""筹资活动现金流量净额"", ""item_field"": ""NETCASHFINA"", ""item_value"": ""-71067506484.81""},
            {""item_title"": ""现金及现金等价物净增加额"", ""item_field"": ""CASHNETR"", ""item_value"": ""19609900305.36""},
            {""item_title"": ""期初现金及现金等价物余额"", ""item_field"": ""BEGPERIOCASH"", ""item_value"": ""147360188952.47""},
            {""item_title"": ""期末现金及现金等价物余额"", ""item_field"": ""ENDPERIOCASH"", ""item_value"": ""164297949257.83""},
            {""item_title"": ""备注字段"", ""item_field"": ""EXTRA_FIELD"", ""item_value"": ""preserved-in-raw""}
          ]
        },
        ""2024-09-30"": {
          ""rType"": ""合并期末"",
          ""data"": [
            {""item_title"": ""销售商品提供劳务收到的现金"", ""item_field"": ""SALESSERVICE"", ""item_value"": ""140000000000.00""},
            {""item_title"": ""经营活动现金流量净额"", ""item_field"": ""MANANETR"", ""item_value"": """"},
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
        Assert.Equal(107536307831.57m, first.TotalOperateOutflow);
#pragma warning disable CS0618 // 测试需要继续访问已过时的 NetcashOperate 以验证 v1.4.0 BREAKING 修正
        // v1.4.0 BREAKING：Sina 路径下 NetcashOperate 取 MANANETR (CFO)，
        // 不再取 CASHNETR (净增加额)。这是 v1.3.x bug 的修正。
        Assert.Equal(92463692168.43m, first.NetcashOperate);
#pragma warning restore CS0618
        Assert.Equal(-1785202630.71m, first.NetcashInvest);
        Assert.Equal(-71067506484.81m, first.NetcashFinance);
        Assert.Equal(147360188952.47m, first.BeginCce);
        Assert.Equal(164297949257.83m, first.EndCce);
        Assert.Equal("Sina", first.Source);

        Assert.NotNull(first.RawFields);
        Assert.Equal("preserved-in-raw", first.RawFields!["EXTRA_FIELD"]);
        Assert.Equal("0.25", first.RawFields["MANANETR_TONGBI"]);
    }

    [Fact]
    public void Parse_OperatingCashFlow_FromMananetr_AndNetCashIncrease_FromCashnetr()
    {
        // 关键：MANANETR 与 CASHNETR 必须被分别映射，不能再被 ??= 共用一个变量。
        var rows = SinaCashFlowSource.ParseResponse(SampleJson, "SH600519");
        var first = rows.Single(r => r.ReportDate == new DateOnly(2024, 12, 31));

        Assert.Equal(92463692168.43m, first.OperatingCashFlow);   // MANANETR
        Assert.Equal(19609900305.36m, first.NetCashIncrease);     // CASHNETR
#pragma warning disable CS0618
        // v1.4.0：NetcashOperate 与 OperatingCashFlow 同值（统一为 CFO）
        Assert.Equal(first.OperatingCashFlow, first.NetcashOperate);
#pragma warning restore CS0618
        // 二者数额显著不同（不能再被错配为同一值）
        Assert.NotEqual(first.NetCashIncrease, first.OperatingCashFlow);
    }

    [Fact]
    public void Parse_MananetrOverridesNetcfoperFallback()
    {
        // 当 MANANETR 与 NETCFOPER 同期出现时，必须以 MANANETR 为准。
        const string Json = @"{
  ""result"": {
    ""status"": {""code"": 0, ""msg"": """"},
    ""data"": {
      ""report_date"": [{""date_value"": ""2024-12-31""}],
      ""report_list"": {
        ""2024-12-31"": {
          ""rType"": ""合并期末"",
          ""data"": [
            {""item_title"": ""经营活动现金流量净额(NETCFOPER)"", ""item_field"": ""NETCFOPER"", ""item_value"": ""1234567.00""},
            {""item_title"": ""经营活动现金流量净额(MANANETR)"", ""item_field"": ""MANANETR"", ""item_value"": ""92463692168.43""}
          ]
        }
      }
    }
  }
}";
        var rows = SinaCashFlowSource.ParseResponse(Json, "SH600519");
        var only = Assert.Single(rows);
        Assert.Equal(92463692168.43m, only.OperatingCashFlow);
    }

    [Fact]
    public void Parse_NetCashIncrease_DerivedWhenCashnetrMissing()
    {
        // CASHNETR 缺失但三大类齐全 → 派生现金净增加额。
        const string Json = @"{
  ""result"": {
    ""status"": {""code"": 0, ""msg"": """"},
    ""data"": {
      ""report_date"": [{""date_value"": ""2024-12-31""}],
      ""report_list"": {
        ""2024-12-31"": {
          ""rType"": ""合并期末"",
          ""data"": [
            {""item_field"": ""MANANETR"", ""item_value"": ""92463692168.43""},
            {""item_field"": ""NETCASHINVE"", ""item_value"": ""-1785202630.71""},
            {""item_field"": ""NETCASHFINA"", ""item_value"": ""-71067506484.81""}
          ]
        }
      }
    }
  }
}";
        var rows = SinaCashFlowSource.ParseResponse(Json, "SH600519");
        var only = Assert.Single(rows);
        var expected = 92463692168.43m + (-1785202630.71m) + (-71067506484.81m);
        Assert.Equal(expected, only.NetCashIncrease);
    }

    [Fact]
    public void Parse_MissingOptionalFields_FieldsAreNull()
    {
        var rows = SinaCashFlowSource.ParseResponse(SampleJson, "SH600519");
        var second = rows.Single(r => r.ReportDate == new DateOnly(2024, 9, 30));

        Assert.Equal(140000000000.00m, second.SalesServices);
#pragma warning disable CS0618
        Assert.Null(second.NetcashOperate);  // MANANETR 空字符串 → null
#pragma warning restore CS0618
        Assert.Null(second.OperatingCashFlow);
        Assert.Null(second.NetCashIncrease);  // CASHNETR 缺失且三大类不全
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
