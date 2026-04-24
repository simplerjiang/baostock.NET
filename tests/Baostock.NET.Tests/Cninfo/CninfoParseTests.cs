using Baostock.NET.Cninfo;
using Baostock.NET.Http;

namespace Baostock.NET.Tests.Cninfo;

/// <summary>
/// <see cref="CninfoSource"/> 纯离线解析 + helper 映射测试（不联网，不依赖 HTTP）。
/// </summary>
public class CninfoParseTests
{
    private const string SampleJson = """
    {
      "totalAnnouncement": 1,
      "totalRecordNum": 1,
      "announcements": [
        {
          "announcementId": "1225114741",
          "secCode": "600519",
          "secName": "贵州茅台",
          "orgId": "gssh0600519",
          "announcementTitle": "贵州茅台2024年年度报告",
          "announcementTime": 1712678400000,
          "adjunctUrl": "finalpage/2026-04-17/1225114741.PDF",
          "adjunctSize": 1892,
          "adjunctType": "PDF",
          "columnId": "125_annualreportpro_content"
        }
      ]
    }
    """;

    [Fact]
    public void Parse_SampleJson_ReturnsExpectedRow()
    {
        var rows = CninfoSource.ParseAnnouncements(SampleJson, "SH600519");

        Assert.Single(rows);
        var r = rows[0];
        Assert.Equal("1225114741", r.AnnouncementId);
        Assert.Equal("SH600519", r.Code);
        Assert.Equal("贵州茅台", r.SecurityName);
        Assert.Equal("贵州茅台2024年年度报告", r.Title);
        Assert.Equal("finalpage/2026-04-17/1225114741.PDF", r.AdjunctUrl);
        Assert.Equal("http://static.cninfo.com.cn/finalpage/2026-04-17/1225114741.PDF", r.FullPdfUrl);
        Assert.Equal("125_annualreportpro_content", r.Category);
        // 1712678400000 ms = 2024-04-09T16:00:00Z → +08 → 2024-04-10
        Assert.Equal(new DateOnly(2024, 4, 10), r.PublishDate);
    }

    [Fact]
    public void Parse_EmptyAnnouncements_ReturnsEmptyList()
    {
        const string body = """{ "totalAnnouncement": 0, "announcements": [] }""";
        var rows = CninfoSource.ParseAnnouncements(body, "SH600519");
        Assert.Empty(rows);
    }

    [Fact]
    public void Parse_MissingAnnouncementsKey_ThrowsDataSourceException()
    {
        const string body = """{ "totalAnnouncement": 0 }""";
        var ex = Assert.Throws<DataSourceException>(() => CninfoSource.ParseAnnouncements(body, "SH600519"));
        Assert.Equal("Cninfo", ex.SourceName);
    }

    [Fact]
    public void Parse_EmptyBody_ThrowsDataSourceException()
    {
        Assert.Throws<DataSourceException>(() => CninfoSource.ParseAnnouncements("   ", "SH600519"));
    }

    [Fact]
    public void Parse_InvalidJson_ThrowsDataSourceException()
    {
        Assert.Throws<DataSourceException>(() => CninfoSource.ParseAnnouncements("not-json", "SH600519"));
    }

    [Theory]
    [InlineData(CninfoAnnouncementCategory.AnnualReport, "category_ndbg_szsh")]
    [InlineData(CninfoAnnouncementCategory.SemiAnnualReport, "category_bndbg_szsh")]
    [InlineData(CninfoAnnouncementCategory.QuarterlyReport, "category_sjdbg_szsh")]
    [InlineData(CninfoAnnouncementCategory.PerformanceForecast, "category_yjygjxz_szsh")]
    [InlineData(CninfoAnnouncementCategory.TemporaryAnnouncement, "category_lshgg_szsh")]
    [InlineData(CninfoAnnouncementCategory.All, "")]
    public void CategoryToParam_AllValues(CninfoAnnouncementCategory category, string expected)
    {
        Assert.Equal(expected, CninfoSource.CategoryToParam(category));
    }

    [Fact]
    public void CodeToColumn_SH600519_ReturnsSse()
        => Assert.Equal("sse", CninfoSource.CodeToColumn("SH600519"));

    [Fact]
    public void CodeToColumn_SZ000001_ReturnsSzse()
        => Assert.Equal("szse", CninfoSource.CodeToColumn("SZ000001"));

    [Fact]
    public void CodeToColumn_BJ430047_ReturnsBj()
        => Assert.Equal("bj", CninfoSource.CodeToColumn("BJ430047"));

    [Fact]
    public void CodeToColumn_InvalidPrefix_Throws()
        => Assert.Throws<ArgumentException>(() => CninfoSource.CodeToColumn("XX123456"));
}
