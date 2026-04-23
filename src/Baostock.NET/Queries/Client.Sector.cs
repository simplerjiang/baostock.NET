using System.Globalization;
using System.Runtime.CompilerServices;
using Baostock.NET.Models;
using Baostock.NET.Protocol;

namespace Baostock.NET.Client;

public partial class BaostockClient
{
    /// <summary>
    /// 查询行业分类。MSG 59/60。code 和 date 均可为空。
    /// </summary>
    /// <param name="code">证券代码，为 <c>null</c> 时查询全部。</param>
    /// <param name="date">查询日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行行业分类数据。</returns>
    public async IAsyncEnumerable<StockIndustryRow> QueryStockIndustryAsync(
        string? code = null,
        string? date = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureLoggedInAsync(ct).ConfigureAwait(false);

        var curPage = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var body = string.Join(Framing.MessageSplit,
                "query_stock_industry",
                Session.UserId ?? "anonymous",
                curPage.ToString(CultureInfo.InvariantCulture),
                Framing.DefaultPerPageCount.ToString(CultureInfo.InvariantCulture),
                code ?? string.Empty,
                date ?? string.Empty);

            var frame = FrameCodec.EncodeFrame(MessageTypes.QueryStockIndustryRequest, body);
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
                yield return new StockIndustryRow(
                    UpdateDate: row[0],
                    Code: row[1],
                    CodeName: row[2],
                    Industry: row[3],
                    IndustryClassification: row[4]);
            }

            if (page.Rows.Count < page.PerPageCount)
                break;

            curPage++;
        }
    }

    /// <summary>
    /// 查询沪深 300 成分股。MSG 61/62。
    /// </summary>
    /// <param name="date">查询日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行成分股数据。</returns>
    public async IAsyncEnumerable<IndexConstituentRow> QueryHs300StocksAsync(
        string? date = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var row in QueryIndexConstituentsAsync(
            "query_hs300_stocks",
            MessageTypes.QueryHs300StocksRequest,
            date, ct).ConfigureAwait(false))
        {
            yield return row;
        }
    }

    /// <summary>
    /// 查询上证 50 成分股。MSG 63/64。
    /// </summary>
    /// <param name="date">查询日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行成分股数据。</returns>
    public async IAsyncEnumerable<IndexConstituentRow> QuerySz50StocksAsync(
        string? date = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var row in QueryIndexConstituentsAsync(
            "query_sz50_stocks",
            MessageTypes.QuerySz50StocksRequest,
            date, ct).ConfigureAwait(false))
        {
            yield return row;
        }
    }

    /// <summary>
    /// 查询中证 500 成分股。MSG 65/66。
    /// </summary>
    /// <param name="date">查询日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行成分股数据。</returns>
    public async IAsyncEnumerable<IndexConstituentRow> QueryZz500StocksAsync(
        string? date = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var row in QueryIndexConstituentsAsync(
            "query_zz500_stocks",
            MessageTypes.QueryZz500StocksRequest,
            date, ct).ConfigureAwait(false))
        {
            yield return row;
        }
    }

    private async IAsyncEnumerable<IndexConstituentRow> QueryIndexConstituentsAsync(
        string method,
        string requestMsgType,
        string? date,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await EnsureLoggedInAsync(ct).ConfigureAwait(false);

        var curPage = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var body = string.Join(Framing.MessageSplit,
                method,
                Session.UserId ?? "anonymous",
                curPage.ToString(CultureInfo.InvariantCulture),
                Framing.DefaultPerPageCount.ToString(CultureInfo.InvariantCulture),
                date ?? string.Empty);

            var frame = FrameCodec.EncodeFrame(requestMsgType, body);
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
                yield return new IndexConstituentRow(
                    UpdateDate: row[0],
                    Code: row[1],
                    CodeName: row[2]);
            }

            if (page.Rows.Count < page.PerPageCount)
                break;

            curPage++;
        }
    }
}
