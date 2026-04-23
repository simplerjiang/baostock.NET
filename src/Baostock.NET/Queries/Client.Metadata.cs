using System.Globalization;
using System.Runtime.CompilerServices;
using Baostock.NET.Models;
using Baostock.NET.Protocol;

namespace Baostock.NET.Client;

public partial class BaostockClient
{
    /// <summary>
    /// 查询交易日信息。MSG 33/34。
    /// </summary>
    /// <param name="startDate">开始日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="endDate">结束日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行交易日数据。</returns>
    public async IAsyncEnumerable<TradeDateRow> QueryTradeDatesAsync(
        string? startDate = null,
        string? endDate = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureLoggedInAsync(ct).ConfigureAwait(false);

        startDate ??= "2015-01-01";
        endDate ??= DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var curPage = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var body = string.Join(Framing.MessageSplit,
                "query_trade_dates",
                Session.UserId ?? "anonymous",
                curPage.ToString(CultureInfo.InvariantCulture),
                Framing.DefaultPerPageCount.ToString(CultureInfo.InvariantCulture),
                startDate,
                endDate);

            var frame = FrameCodec.EncodeFrame(MessageTypes.QueryTradeDatesRequest, body);
            await _transport.SendAsync(frame, ct).ConfigureAwait(false);

            var responseFrame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
            var (_, respBody) = DecodeResponseFrame(responseFrame);

            var page = ResponseParser.ParsePage(respBody);

            if (!string.Equals(page.ErrorCode, "0", StringComparison.Ordinal))
            {
                throw new BaostockException(page.ErrorCode, page.ErrorMessage);
            }

            foreach (var row in page.Rows)
            {
                yield return new TradeDateRow(
                    DateOnly.ParseExact(row[0], "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    row[1] == "1");
            }

            if (page.Rows.Count < page.PerPageCount)
                break;

            curPage++;
        }
    }

    /// <summary>
    /// 查询指定日期的全部证券列表。MSG 35/36。
    /// </summary>
    /// <param name="day">查询日期，格式 <c>"yyyy-MM-dd"</c>；为 <c>null</c> 时默认当天。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行证券列表数据。</returns>
    public async IAsyncEnumerable<StockListRow> QueryAllStockAsync(
        string? day = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureLoggedInAsync(ct).ConfigureAwait(false);

        day ??= DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var curPage = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var body = string.Join(Framing.MessageSplit,
                "query_all_stock",
                Session.UserId ?? "anonymous",
                curPage.ToString(CultureInfo.InvariantCulture),
                Framing.DefaultPerPageCount.ToString(CultureInfo.InvariantCulture),
                day);

            var frame = FrameCodec.EncodeFrame(MessageTypes.QueryAllStockRequest, body);
            await _transport.SendAsync(frame, ct).ConfigureAwait(false);

            var responseFrame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
            var (_, respBody) = DecodeResponseFrame(responseFrame);

            var page = ResponseParser.ParsePage(respBody);

            if (!string.Equals(page.ErrorCode, "0", StringComparison.Ordinal))
            {
                throw new BaostockException(page.ErrorCode, page.ErrorMessage);
            }

            foreach (var row in page.Rows)
            {
                yield return new StockListRow(
                    Code: row[0],
                    TradeStatus: row[1],
                    CodeName: row[2]);
            }

            if (page.Rows.Count < page.PerPageCount)
                break;

            curPage++;
        }
    }

    /// <summary>
    /// 查询证券基本资料。MSG 45/46。code 和 code_name 可同时为空（返回全部），code_name 支持模糊查询。
    /// </summary>
    /// <param name="code">证券代码，为 <c>null</c> 时不按代码筛选。</param>
    /// <param name="codeName">证券名称（支持模糊查询），为 <c>null</c> 时不按名称筛选。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行证券基本资料数据。</returns>
    public async IAsyncEnumerable<StockBasicRow> QueryStockBasicAsync(
        string? code = null,
        string? codeName = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureLoggedInAsync(ct).ConfigureAwait(false);

        var curPage = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var body = string.Join(Framing.MessageSplit,
                "query_stock_basic",
                Session.UserId ?? "anonymous",
                curPage.ToString(CultureInfo.InvariantCulture),
                Framing.DefaultPerPageCount.ToString(CultureInfo.InvariantCulture),
                code ?? string.Empty,
                codeName ?? string.Empty);

            var frame = FrameCodec.EncodeFrame(MessageTypes.QueryStockBasicRequest, body);
            await _transport.SendAsync(frame, ct).ConfigureAwait(false);

            var responseFrame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
            var (_, respBody) = DecodeResponseFrame(responseFrame);

            var page = ResponseParser.ParsePage(respBody);

            if (!string.Equals(page.ErrorCode, "0", StringComparison.Ordinal))
            {
                throw new BaostockException(page.ErrorCode, page.ErrorMessage);
            }

            foreach (var row in page.Rows)
            {
                yield return new StockBasicRow(
                    Code: row[0],
                    CodeName: row[1],
                    IpoDate: string.IsNullOrEmpty(row[2]) ? null : row[2],
                    OutDate: string.IsNullOrEmpty(row[3]) ? null : row[3],
                    Type: row[4],
                    Status: row[5]);
            }

            if (page.Rows.Count < page.PerPageCount)
                break;

            curPage++;
        }
    }
}
