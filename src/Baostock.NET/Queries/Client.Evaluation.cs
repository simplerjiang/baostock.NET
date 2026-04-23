using System.Globalization;
using System.Runtime.CompilerServices;
using Baostock.NET.Models;
using Baostock.NET.Protocol;

namespace Baostock.NET.Client;

public partial class BaostockClient
{
    /// <summary>
    /// 查询股息分红。MSG 13/14。
    /// </summary>
    /// <param name="code">证券代码，如 <c>"sh.600000"</c>。</param>
    /// <param name="year">年份，如 <c>"2024"</c>；为 <c>null</c> 时默认当年。</param>
    /// <param name="yearType">年份类型，<c>"report"</c> 表示报告期，<c>"operate"</c> 表示实施日期。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行股息分红数据。</returns>
    public async IAsyncEnumerable<DividendRow> QueryDividendDataAsync(
        string code,
        string? year = null,
        string yearType = "report",
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureLoggedInAsync(ct).ConfigureAwait(false);

        ArgumentException.ThrowIfNullOrEmpty(code);
        code = code.ToLowerInvariant();
        year ??= DateTime.Now.ToString("yyyy", CultureInfo.InvariantCulture);

        var curPage = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var body = string.Join(Framing.MessageSplit,
                "query_dividend_data",
                Session.UserId ?? "anonymous",
                curPage.ToString(CultureInfo.InvariantCulture),
                Framing.DefaultPerPageCount.ToString(CultureInfo.InvariantCulture),
                code,
                year,
                yearType);

            var frame = FrameCodec.EncodeFrame(MessageTypes.QueryDividendDataRequest, body);
            await _transport.SendAsync(frame, ct).ConfigureAwait(false);

            var responseFrame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
            var (_, respBody) = DecodeResponseFrame(responseFrame);

            var page = ResponseParser.ParsePage(respBody);

            if (!string.Equals(page.ErrorCode, "0", StringComparison.Ordinal))
                throw new BaostockException(page.ErrorCode, page.ErrorMessage);

            foreach (var row in page.Rows)
            {
                yield return new DividendRow(
                    Code: row[0],
                    DividPreNoticeDate: NullIfEmpty(row[1]),
                    DividAgmPumDate: NullIfEmpty(row[2]),
                    DividPlanAnnounceDate: NullIfEmpty(row[3]),
                    DividPlanDate: NullIfEmpty(row[4]),
                    DividRegistDate: NullIfEmpty(row[5]),
                    DividOperateDate: NullIfEmpty(row[6]),
                    DividPayDate: NullIfEmpty(row[7]),
                    DividStockMarketDate: NullIfEmpty(row[8]),
                    DividCashPsBeforeTax: NullIfEmpty(row[9]),
                    DividCashPsAfterTax: NullIfEmpty(row[10]),
                    DividStocksPs: NullIfEmpty(row[11]),
                    DividCashStock: NullIfEmpty(row[12]),
                    DividReserveToStockPs: NullIfEmpty(row[13]));
            }

            if (page.Rows.Count < page.PerPageCount) break;
            curPage++;
        }
    }

    /// <summary>
    /// 查询复权因子。MSG 15/16。
    /// </summary>
    /// <param name="code">证券代码，如 <c>"sh.600000"</c>。</param>
    /// <param name="startDate">开始日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="endDate">结束日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行复权因子数据。</returns>
    public async IAsyncEnumerable<AdjustFactorRow> QueryAdjustFactorAsync(
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
                "query_adjust_factor",
                Session.UserId ?? "anonymous",
                curPage.ToString(CultureInfo.InvariantCulture),
                Framing.DefaultPerPageCount.ToString(CultureInfo.InvariantCulture),
                code,
                startDate,
                endDate);

            var frame = FrameCodec.EncodeFrame(MessageTypes.AdjustFactorRequest, body);
            await _transport.SendAsync(frame, ct).ConfigureAwait(false);

            var responseFrame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
            var (_, respBody) = DecodeResponseFrame(responseFrame);

            var page = ResponseParser.ParsePage(respBody);

            if (!string.Equals(page.ErrorCode, "0", StringComparison.Ordinal))
                throw new BaostockException(page.ErrorCode, page.ErrorMessage);

            foreach (var row in page.Rows)
            {
                yield return new AdjustFactorRow(
                    Code: row[0],
                    DividOperateDate: NullIfEmpty(row[1]),
                    ForeAdjustFactor: ParseNullableDecimal(row[2]),
                    BackAdjustFactor: ParseNullableDecimal(row[3]),
                    AdjustFactor: ParseNullableDecimal(row[4]));
            }

            if (page.Rows.Count < page.PerPageCount) break;
            curPage++;
        }
    }

    /// <summary>查询季频盈利能力。MSG 17/18。</summary>
    /// <param name="code">证券代码，如 <c>"sh.600000"</c>。</param>
    /// <param name="year">年份，如 2024。</param>
    /// <param name="quarter">季度（1–4）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行盈利能力数据。</returns>
    public IAsyncEnumerable<ProfitRow> QueryProfitDataAsync(
        string code, int year, int quarter, CancellationToken ct = default)
        => QuerySeasonAsync("query_profit_data", MessageTypes.ProfitDataRequest,
            code, year, quarter, ParseProfitRow, ct);

    /// <summary>查询季频营运能力。MSG 19/20。</summary>
    /// <param name="code">证券代码，如 <c>"sh.600000"</c>。</param>
    /// <param name="year">年份，如 2024。</param>
    /// <param name="quarter">季度（1–4）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行营运能力数据。</returns>
    public IAsyncEnumerable<OperationRow> QueryOperationDataAsync(
        string code, int year, int quarter, CancellationToken ct = default)
        => QuerySeasonAsync("query_operation_data", MessageTypes.OperationDataRequest,
            code, year, quarter, ParseOperationRow, ct);

    /// <summary>查询季频成长能力。MSG 21/22。</summary>
    /// <param name="code">证券代码，如 <c>"sh.600000"</c>。</param>
    /// <param name="year">年份，如 2024。</param>
    /// <param name="quarter">季度（1–4）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行成长能力数据。</returns>
    public IAsyncEnumerable<GrowthRow> QueryGrowthDataAsync(
        string code, int year, int quarter, CancellationToken ct = default)
        => QuerySeasonAsync("query_growth_data", MessageTypes.QueryGrowthDataRequest,
            code, year, quarter, ParseGrowthRow, ct);

    /// <summary>查询季频杜邦指数。MSG 23/24。</summary>
    /// <param name="code">证券代码，如 <c>"sh.600000"</c>。</param>
    /// <param name="year">年份，如 2024。</param>
    /// <param name="quarter">季度（1–4）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行杜邦指数数据。</returns>
    public IAsyncEnumerable<DupontRow> QueryDupontDataAsync(
        string code, int year, int quarter, CancellationToken ct = default)
        => QuerySeasonAsync("query_dupont_data", MessageTypes.QueryDupontDataRequest,
            code, year, quarter, ParseDupontRow, ct);

    /// <summary>查询季频偿债能力。MSG 25/26。</summary>
    /// <param name="code">证券代码，如 <c>"sh.600000"</c>。</param>
    /// <param name="year">年份，如 2024。</param>
    /// <param name="quarter">季度（1–4）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行偿债能力数据。</returns>
    public IAsyncEnumerable<BalanceRow> QueryBalanceDataAsync(
        string code, int year, int quarter, CancellationToken ct = default)
        => QuerySeasonAsync("query_balance_data", MessageTypes.QueryBalanceDataRequest,
            code, year, quarter, ParseBalanceRow, ct);

    /// <summary>查询季频现金流量。MSG 27/28。</summary>
    /// <param name="code">证券代码，如 <c>"sh.600000"</c>。</param>
    /// <param name="year">年份，如 2024。</param>
    /// <param name="quarter">季度（1–4）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行现金流量数据。</returns>
    public IAsyncEnumerable<CashFlowRow> QueryCashFlowDataAsync(
        string code, int year, int quarter, CancellationToken ct = default)
        => QuerySeasonAsync("query_cash_flow_data", MessageTypes.QueryCashFlowDataRequest,
            code, year, quarter, ParseCashFlowRow, ct);

    // ── 私有辅助 ──────────────────────────────────────

    private async IAsyncEnumerable<T> QuerySeasonAsync<T>(
        string method,
        string requestMsgType,
        string code,
        int year,
        int quarter,
        Func<string[], T> parseRow,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await EnsureLoggedInAsync(ct).ConfigureAwait(false);

        ArgumentException.ThrowIfNullOrEmpty(code);
        code = code.ToLowerInvariant();

        var curPage = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var body = string.Join(Framing.MessageSplit,
                method,
                Session.UserId ?? "anonymous",
                curPage.ToString(CultureInfo.InvariantCulture),
                Framing.DefaultPerPageCount.ToString(CultureInfo.InvariantCulture),
                code,
                year.ToString(CultureInfo.InvariantCulture),
                quarter.ToString(CultureInfo.InvariantCulture));

            var frame = FrameCodec.EncodeFrame(requestMsgType, body);
            await _transport.SendAsync(frame, ct).ConfigureAwait(false);

            var responseFrame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
            var (_, respBody) = DecodeResponseFrame(responseFrame);

            var page = ResponseParser.ParsePage(respBody);

            if (!string.Equals(page.ErrorCode, "0", StringComparison.Ordinal))
                throw new BaostockException(page.ErrorCode, page.ErrorMessage);

            foreach (var row in page.Rows)
                yield return parseRow(row);

            if (page.Rows.Count < page.PerPageCount) break;
            curPage++;
        }
    }

    private static string? NullIfEmpty(string value)
        => string.IsNullOrEmpty(value) ? null : value;

    private static ProfitRow ParseProfitRow(string[] cols) => new(
        Code: cols[0],
        PubDate: NullIfEmpty(cols[1]),
        StatDate: NullIfEmpty(cols[2]),
        RoeAvg: ParseNullableDecimal(cols[3]),
        NpMargin: ParseNullableDecimal(cols[4]),
        GpMargin: ParseNullableDecimal(cols[5]),
        NetProfit: ParseNullableDecimal(cols[6]),
        EpsTtm: ParseNullableDecimal(cols[7]),
        MbRevenue: ParseNullableDecimal(cols[8]),
        TotalShare: ParseNullableDecimal(cols[9]),
        LiqaShare: ParseNullableDecimal(cols[10]));

    private static OperationRow ParseOperationRow(string[] cols) => new(
        Code: cols[0],
        PubDate: NullIfEmpty(cols[1]),
        StatDate: NullIfEmpty(cols[2]),
        NrTurnRatio: ParseNullableDecimal(cols[3]),
        NrTurnDays: ParseNullableDecimal(cols[4]),
        InvTurnRatio: ParseNullableDecimal(cols[5]),
        InvTurnDays: ParseNullableDecimal(cols[6]),
        CaTurnRatio: ParseNullableDecimal(cols[7]),
        AssetTurnRatio: ParseNullableDecimal(cols[8]));

    private static GrowthRow ParseGrowthRow(string[] cols) => new(
        Code: cols[0],
        PubDate: NullIfEmpty(cols[1]),
        StatDate: NullIfEmpty(cols[2]),
        YoyEquity: ParseNullableDecimal(cols[3]),
        YoyAsset: ParseNullableDecimal(cols[4]),
        YoyNi: ParseNullableDecimal(cols[5]),
        YoyEpsBasic: ParseNullableDecimal(cols[6]),
        YoyPni: ParseNullableDecimal(cols[7]));

    private static DupontRow ParseDupontRow(string[] cols) => new(
        Code: cols[0],
        PubDate: NullIfEmpty(cols[1]),
        StatDate: NullIfEmpty(cols[2]),
        DupontRoe: ParseNullableDecimal(cols[3]),
        DupontAssetStoEquity: ParseNullableDecimal(cols[4]),
        DupontAssetTurn: ParseNullableDecimal(cols[5]),
        DupontPnitoni: ParseNullableDecimal(cols[6]),
        DupontNitogr: ParseNullableDecimal(cols[7]),
        DupontTaxBurden: ParseNullableDecimal(cols[8]),
        DupontIntburden: ParseNullableDecimal(cols[9]),
        DupontEbittogr: ParseNullableDecimal(cols[10]));

    private static BalanceRow ParseBalanceRow(string[] cols) => new(
        Code: cols[0],
        PubDate: NullIfEmpty(cols[1]),
        StatDate: NullIfEmpty(cols[2]),
        CurrentRatio: ParseNullableDecimal(cols[3]),
        QuickRatio: ParseNullableDecimal(cols[4]),
        CashRatio: ParseNullableDecimal(cols[5]),
        YoyLiability: ParseNullableDecimal(cols[6]),
        LiabilityToAsset: ParseNullableDecimal(cols[7]),
        AssetToEquity: ParseNullableDecimal(cols[8]));

    private static CashFlowRow ParseCashFlowRow(string[] cols) => new(
        Code: cols[0],
        PubDate: NullIfEmpty(cols[1]),
        StatDate: NullIfEmpty(cols[2]),
        CaToAsset: ParseNullableDecimal(cols[3]),
        NcaToAsset: ParseNullableDecimal(cols[4]),
        TangibleAssetToAsset: ParseNullableDecimal(cols[5]),
        EbitToInterest: ParseNullableDecimal(cols[6]),
        CfoToOr: ParseNullableDecimal(cols[7]),
        CfoToNp: ParseNullableDecimal(cols[8]),
        CfoToGr: ParseNullableDecimal(cols[9]));
}
