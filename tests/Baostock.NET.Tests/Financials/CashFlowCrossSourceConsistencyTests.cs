using Baostock.NET.Financials;

namespace Baostock.NET.Tests.Financials;

/// <summary>
/// v1.4.0 跨源一致性测试：用同一组"语义事实"（CFO=92.46, CFI=-1.79, CFF=-71.07,
/// 净增加额=19.61）但字段命名风格不同的两份 fixture（EM 风格 + Sina 风格）
/// 走两个 source 的解析路径，断言关键派生字段在数额上一致。
/// 这是防止 v1.3.4 那种"跨源语义错位"再次发生的保险。
/// </summary>
public class CashFlowCrossSourceConsistencyTests
{
    private const decimal Cfo = 92463692168.43m;
    private const decimal Cfi = -1785202630.71m;
    private const decimal Cff = -71067506484.81m;
    private const decimal CashIncrease = 19609900305.36m; // Sina CASHNETR 实测；EM 派生 ≈ Cfo+Cfi+Cff = 19,610,983,052.91

    private const string EmJson = @"{
  ""data"": [
    {
      ""SECUCODE"": ""600519.SH"",
      ""REPORT_DATE"": ""2024-12-31 00:00:00"",
      ""REPORT_TYPE"": ""年报"",
      ""NETCASH_OPERATE"": ""92463692168.43"",
      ""NETCASH_INVEST"": ""-1785202630.71"",
      ""NETCASH_FINANCE"": ""-71067506484.81"",
      ""BEGIN_CCE"": ""147360188952.47"",
      ""END_CCE"": ""164297949257.83""
    }
  ]
}";

    private const string SinaJson = @"{
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
            {""item_field"": ""NETCASHFINA"", ""item_value"": ""-71067506484.81""},
            {""item_field"": ""CASHNETR"", ""item_value"": ""19609900305.36""},
            {""item_field"": ""BEGPERIOCASH"", ""item_value"": ""147360188952.47""},
            {""item_field"": ""ENDPERIOCASH"", ""item_value"": ""164297949257.83""}
          ]
        }
      }
    }
  }
}";

    [Fact]
    public void OperatingCashFlow_AgreesAcrossSources()
    {
        var em = EastMoneyCashFlowSource.ParseResponse(EmJson, "SH600519").Single();
        var sina = SinaCashFlowSource.ParseResponse(SinaJson, "SH600519").Single();

        Assert.Equal(Cfo, em.OperatingCashFlow);
        Assert.Equal(Cfo, sina.OperatingCashFlow);
        Assert.Equal(em.OperatingCashFlow, sina.OperatingCashFlow);
    }

    [Fact]
    public void NetcashOperate_AgreesAcrossSources_BothEqualToCfo()
    {
        var em = EastMoneyCashFlowSource.ParseResponse(EmJson, "SH600519").Single();
        var sina = SinaCashFlowSource.ParseResponse(SinaJson, "SH600519").Single();

#pragma warning disable CS0618
        Assert.Equal(Cfo, em.NetcashOperate);
        Assert.Equal(Cfo, sina.NetcashOperate);
        Assert.Equal(em.NetcashOperate, sina.NetcashOperate);
        // NetcashOperate 与 OperatingCashFlow 在两路径下都同值
        Assert.Equal(em.NetcashOperate, em.OperatingCashFlow);
        Assert.Equal(sina.NetcashOperate, sina.OperatingCashFlow);
#pragma warning restore CS0618
    }

    [Fact]
    public void NetcashInvestAndFinance_AgreeAcrossSources()
    {
        var em = EastMoneyCashFlowSource.ParseResponse(EmJson, "SH600519").Single();
        var sina = SinaCashFlowSource.ParseResponse(SinaJson, "SH600519").Single();

        Assert.Equal(Cfi, em.NetcashInvest);
        Assert.Equal(Cfi, sina.NetcashInvest);
        Assert.Equal(Cff, em.NetcashFinance);
        Assert.Equal(Cff, sina.NetcashFinance);
    }

    [Fact]
    public void NetCashIncrease_AgreesAcrossSources_WithinDerivationTolerance()
    {
        // EM 路径下 NetCashIncrease = CFO+CFI+CFF（派生），Sina 路径下取 CASHNETR（直读）。
        // 二者在真实接口里数额可能小幅不同（来自 Sina 的"现金及其等价物的影响"等额外调节项），
        // 但应在 1% 容差内一致。
        var em = EastMoneyCashFlowSource.ParseResponse(EmJson, "SH600519").Single();
        var sina = SinaCashFlowSource.ParseResponse(SinaJson, "SH600519").Single();

        Assert.NotNull(em.NetCashIncrease);
        Assert.NotNull(sina.NetCashIncrease);

        var emValue = em.NetCashIncrease!.Value;
        var sinaValue = sina.NetCashIncrease!.Value;

        // EM 派生值 ≈ 19,610,983,052.91；Sina CASHNETR = 19,609,900,305.36
        // 差异 / 量级 < 1%。
        var diff = Math.Abs(emValue - sinaValue);
        var tolerance = Math.Abs(sinaValue) * 0.01m;
        Assert.True(diff <= tolerance,
            $"NetCashIncrease 跨源差异过大：EM={emValue} Sina={sinaValue} diff={diff} tol={tolerance}");
    }
}
