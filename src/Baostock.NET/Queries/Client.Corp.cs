using System.Globalization;
using System.Runtime.CompilerServices;
using Baostock.NET.Models;
using Baostock.NET.Protocol;

namespace Baostock.NET.Client;

public partial class BaostockClient
{
    /// <summary>
    /// 查询公司业绩快报。MSG 29/30。
    /// </summary>
    /// <param name="code">证券代码，如 <c>"sh.600000"</c>。</param>
    /// <param name="startDate">开始日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="endDate">结束日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行业绩快报数据。</returns>
    public async IAsyncEnumerable<PerformanceExpressRow> QueryPerformanceExpressReportAsync(
        string code,
        string? startDate = null,
        string? endDate = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureLoggedInAsync(ct).ConfigureAwait(false);

        ArgumentException.ThrowIfNullOrEmpty(code);
        code = code.ToLowerInvariant();
        startDate ??= "2015-01-01";
        endDate ??= DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var curPage = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var body = string.Join(Framing.MessageSplit,
                "query_performance_express_report",
                Session.UserId ?? "anonymous",
                curPage.ToString(CultureInfo.InvariantCulture),
                Framing.DefaultPerPageCount.ToString(CultureInfo.InvariantCulture),
                code,
                startDate,
                endDate);

            var frame = FrameCodec.EncodeFrame(MessageTypes.QueryPerformanceExpressReportRequest, body);
            await _transport.SendAsync(frame, ct).ConfigureAwait(false);

            var responseFrame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
            var (_, respBody) = DecodeResponseFrame(responseFrame);

            var page = ResponseParser.ParsePage(respBody);

            if (!string.Equals(page.ErrorCode, "0", StringComparison.Ordinal))
                throw new BaostockException(page.ErrorCode, page.ErrorMessage);

            foreach (var row in page.Rows)
            {
                yield return new PerformanceExpressRow(
                    Code: row[0],
                    PerformanceExpPubDate: NullIfEmpty(row[1]),
                    PerformanceExpStatDate: NullIfEmpty(row[2]),
                    PerformanceExpUpdateDate: NullIfEmpty(row[3]),
                    PerformanceExpressTotalAsset: NullIfEmpty(row[4]),
                    PerformanceExpressNetAsset: NullIfEmpty(row[5]),
                    PerformanceExpressEPSChgPct: NullIfEmpty(row[6]),
                    PerformanceExpressROEWa: NullIfEmpty(row[7]),
                    PerformanceExpressEPSDiluted: NullIfEmpty(row[8]),
                    PerformanceExpressGRYOY: NullIfEmpty(row[9]),
                    PerformanceExpressOPYOY: NullIfEmpty(row[10]));
            }

            if (page.Rows.Count < page.PerPageCount) break;
            curPage++;
        }
    }

    /// <summary>
    /// 查询公司业绩预告。MSG 31/32。
    /// </summary>
    /// <param name="code">证券代码，如 <c>"sh.600000"</c>。</param>
    /// <param name="startDate">开始日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="endDate">结束日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行业绩预告数据。</returns>
    public async IAsyncEnumerable<ForecastReportRow> QueryForecastReportAsync(
        string code,
        string? startDate = null,
        string? endDate = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureLoggedInAsync(ct).ConfigureAwait(false);

        ArgumentException.ThrowIfNullOrEmpty(code);
        code = code.ToLowerInvariant();
        startDate ??= "2015-01-01";
        endDate ??= DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var curPage = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var body = string.Join(Framing.MessageSplit,
                "query_forecast_report",
                Session.UserId ?? "anonymous",
                curPage.ToString(CultureInfo.InvariantCulture),
                Framing.DefaultPerPageCount.ToString(CultureInfo.InvariantCulture),
                code,
                startDate,
                endDate);

            var frame = FrameCodec.EncodeFrame(MessageTypes.QueryForecastReportRequest, body);
            await _transport.SendAsync(frame, ct).ConfigureAwait(false);

            var responseFrame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
            var (_, respBody) = DecodeResponseFrame(responseFrame);

            var page = ResponseParser.ParsePage(respBody);

            if (!string.Equals(page.ErrorCode, "0", StringComparison.Ordinal))
                throw new BaostockException(page.ErrorCode, page.ErrorMessage);

            foreach (var row in page.Rows)
            {
                yield return new ForecastReportRow(
                    Code: row[0],
                    ProfitForcastExpPubDate: NullIfEmpty(row[1]),
                    ProfitForcastExpStatDate: NullIfEmpty(row[2]),
                    ProfitForcastType: NullIfEmpty(row[3]),
                    ProfitForcastAbstract: NullIfEmpty(row[4]),
                    ProfitForcastChgPctUp: NullIfEmpty(row[5]),
                    ProfitForcastChgPctDwn: NullIfEmpty(row[6]));
            }

            if (page.Rows.Count < page.PerPageCount) break;
            curPage++;
        }
    }
}
