using System.Globalization;
using System.Runtime.CompilerServices;
using Baostock.NET.Models;
using Baostock.NET.Protocol;
using Baostock.NET.Util;

namespace Baostock.NET.Client;

public partial class BaostockClient
{
    /// <summary>
    /// 查询终止上市股票列表。MSG 67/68。
    /// </summary>
    /// <param name="date">查询日期，格式 <c>"yyyy-MM-dd"</c>；为 <c>null</c> 时使用服务端默认值。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行终止上市股票数据。</returns>
    public async IAsyncEnumerable<SpecialStockRow> QueryTerminatedStocksAsync(
        string? date = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var row in QuerySpecialStocksAsync(
            "query_terminated_stocks",
            MessageTypes.QueryTerminatedStocksRequest,
            date, ct).ConfigureAwait(false))
        {
            yield return row;
        }
    }

    /// <summary>
    /// 查询暂停上市股票列表。MSG 69/70。
    /// </summary>
    /// <param name="date">查询日期，格式 <c>"yyyy-MM-dd"</c>；为 <c>null</c> 时使用服务端默认值。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行暂停上市股票数据。</returns>
    public async IAsyncEnumerable<SpecialStockRow> QuerySuspendedStocksAsync(
        string? date = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var row in QuerySpecialStocksAsync(
            "query_suspended_stocks",
            MessageTypes.QuerySuspendedStocksRequest,
            date, ct).ConfigureAwait(false))
        {
            yield return row;
        }
    }

    /// <summary>
    /// 查询 ST 股票列表。MSG 71/72。
    /// </summary>
    /// <param name="date">查询日期，格式 <c>"yyyy-MM-dd"</c>；为 <c>null</c> 时使用服务端默认值。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行 ST 股票数据。</returns>
    public async IAsyncEnumerable<SpecialStockRow> QueryStStocksAsync(
        string? date = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var row in QuerySpecialStocksAsync(
            "query_st_stocks",
            MessageTypes.QueryStStocksRequest,
            date, ct).ConfigureAwait(false))
        {
            yield return row;
        }
    }

    /// <summary>
    /// 查询 *ST 股票列表。MSG 73/74。
    /// </summary>
    /// <param name="date">查询日期，格式 <c>"yyyy-MM-dd"</c>；为 <c>null</c> 时使用服务端默认值。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行 *ST 股票数据。</returns>
    public async IAsyncEnumerable<SpecialStockRow> QueryStarStStocksAsync(
        string? date = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var row in QuerySpecialStocksAsync(
            "query_starst_stocks",
            MessageTypes.QueryStarStStocksRequest,
            date, ct).ConfigureAwait(false))
        {
            yield return row;
        }
    }

    private async IAsyncEnumerable<SpecialStockRow> QuerySpecialStocksAsync(
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
                yield return new SpecialStockRow(
                    UpdateDate: row[0],
                    Code: FormatModelCode(row[1]),
                    CodeName: row[2]);
            }

            if (page.Rows.Count < page.PerPageCount)
                break;

            curPage++;
        }
    }
}
