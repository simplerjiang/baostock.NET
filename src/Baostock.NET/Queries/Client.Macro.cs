using System.Globalization;
using System.Runtime.CompilerServices;
using Baostock.NET.Models;
using Baostock.NET.Protocol;

namespace Baostock.NET.Client;

public partial class BaostockClient
{
    /// <summary>
    /// 查询存款利率。MSG 47/48。
    /// </summary>
    /// <param name="startDate">开始日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="endDate">结束日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行存款利率数据。</returns>
    public async IAsyncEnumerable<DepositRateRow> QueryDepositRateDataAsync(
        string? startDate = null,
        string? endDate = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureLoggedInAsync(ct).ConfigureAwait(false);

        var curPage = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var body = string.Join(Framing.MessageSplit,
                "query_deposit_rate_data",
                Session.UserId ?? "anonymous",
                curPage.ToString(CultureInfo.InvariantCulture),
                Framing.DefaultPerPageCount.ToString(CultureInfo.InvariantCulture),
                startDate ?? string.Empty,
                endDate ?? string.Empty);

            var frame = FrameCodec.EncodeFrame(MessageTypes.QueryDepositRateDataRequest, body);
            await _transport.SendAsync(frame, ct).ConfigureAwait(false);

            var responseFrame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
            var (_, respBody) = DecodeResponseFrame(responseFrame);

            var page = ResponseParser.ParsePage(respBody);

            if (!string.Equals(page.ErrorCode, "0", StringComparison.Ordinal))
                throw new BaostockException(page.ErrorCode, page.ErrorMessage);

            foreach (var row in page.Rows)
            {
                yield return new DepositRateRow(
                    PubDate: NullIfEmpty(row[0]),
                    DemandDepositRate: NullIfEmpty(row[1]),
                    FixedDepositRate3Month: NullIfEmpty(row[2]),
                    FixedDepositRate6Month: NullIfEmpty(row[3]),
                    FixedDepositRate1Year: NullIfEmpty(row[4]),
                    FixedDepositRate2Year: NullIfEmpty(row[5]),
                    FixedDepositRate3Year: NullIfEmpty(row[6]),
                    FixedDepositRate5Year: NullIfEmpty(row[7]),
                    InstallmentFixedDepositRate1Year: NullIfEmpty(row[8]),
                    InstallmentFixedDepositRate3Year: NullIfEmpty(row[9]),
                    InstallmentFixedDepositRate5Year: NullIfEmpty(row[10]));
            }

            if (page.Rows.Count < page.PerPageCount) break;
            curPage++;
        }
    }

    /// <summary>
    /// 查询贷款利率。MSG 49/50。
    /// </summary>
    /// <param name="startDate">开始日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="endDate">结束日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行贷款利率数据。</returns>
    public async IAsyncEnumerable<LoanRateRow> QueryLoanRateDataAsync(
        string? startDate = null,
        string? endDate = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureLoggedInAsync(ct).ConfigureAwait(false);

        var curPage = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var body = string.Join(Framing.MessageSplit,
                "query_loan_rate_data",
                Session.UserId ?? "anonymous",
                curPage.ToString(CultureInfo.InvariantCulture),
                Framing.DefaultPerPageCount.ToString(CultureInfo.InvariantCulture),
                startDate ?? string.Empty,
                endDate ?? string.Empty);

            var frame = FrameCodec.EncodeFrame(MessageTypes.QueryLoanRateDataRequest, body);
            await _transport.SendAsync(frame, ct).ConfigureAwait(false);

            var responseFrame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
            var (_, respBody) = DecodeResponseFrame(responseFrame);

            var page = ResponseParser.ParsePage(respBody);

            if (!string.Equals(page.ErrorCode, "0", StringComparison.Ordinal))
                throw new BaostockException(page.ErrorCode, page.ErrorMessage);

            foreach (var row in page.Rows)
            {
                yield return new LoanRateRow(
                    PubDate: NullIfEmpty(row[0]),
                    LoanRate6Month: NullIfEmpty(row[1]),
                    LoanRate6MonthTo1Year: NullIfEmpty(row[2]),
                    LoanRate1YearTo3Year: NullIfEmpty(row[3]),
                    LoanRate3YearTo5Year: NullIfEmpty(row[4]),
                    LoanRateAbove5Year: NullIfEmpty(row[5]),
                    MortgateRateBelow5Year: NullIfEmpty(row[6]),
                    MortgateRateAbove5Year: NullIfEmpty(row[7]));
            }

            if (page.Rows.Count < page.PerPageCount) break;
            curPage++;
        }
    }

    /// <summary>
    /// 查询存款准备金率。MSG 51/52。
    /// </summary>
    /// <param name="startDate">开始日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="endDate">结束日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="yearType">年份类型，默认 <c>"0"</c>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行存款准备金率数据。</returns>
    public async IAsyncEnumerable<ReserveRatioRow> QueryRequiredReserveRatioDataAsync(
        string? startDate = null,
        string? endDate = null,
        string yearType = "0",
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureLoggedInAsync(ct).ConfigureAwait(false);

        var curPage = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var body = string.Join(Framing.MessageSplit,
                "query_required_reserve_ratio_data",
                Session.UserId ?? "anonymous",
                curPage.ToString(CultureInfo.InvariantCulture),
                Framing.DefaultPerPageCount.ToString(CultureInfo.InvariantCulture),
                startDate ?? string.Empty,
                endDate ?? string.Empty,
                yearType);

            var frame = FrameCodec.EncodeFrame(MessageTypes.QueryRequiredReserveRatioDataRequest, body);
            await _transport.SendAsync(frame, ct).ConfigureAwait(false);

            var responseFrame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
            var (_, respBody) = DecodeResponseFrame(responseFrame);

            var page = ResponseParser.ParsePage(respBody);

            if (!string.Equals(page.ErrorCode, "0", StringComparison.Ordinal))
                throw new BaostockException(page.ErrorCode, page.ErrorMessage);

            foreach (var row in page.Rows)
            {
                yield return new ReserveRatioRow(
                    PubDate: NullIfEmpty(row[0]),
                    EffectiveDate: NullIfEmpty(row[1]),
                    BigInstitutionsRatioPre: NullIfEmpty(row[2]),
                    BigInstitutionsRatioAfter: NullIfEmpty(row[3]),
                    MediumInstitutionsRatioPre: NullIfEmpty(row[4]),
                    MediumInstitutionsRatioAfter: NullIfEmpty(row[5]));
            }

            if (page.Rows.Count < page.PerPageCount) break;
            curPage++;
        }
    }

    /// <summary>
    /// 查询货币供应量（月度）。MSG 53/54。
    /// </summary>
    /// <param name="startDate">开始日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="endDate">结束日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行货币供应量月度数据。</returns>
    public async IAsyncEnumerable<MoneySupplyMonthRow> QueryMoneySupplyDataMonthAsync(
        string? startDate = null,
        string? endDate = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureLoggedInAsync(ct).ConfigureAwait(false);

        var curPage = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var body = string.Join(Framing.MessageSplit,
                "query_money_supply_data_month",
                Session.UserId ?? "anonymous",
                curPage.ToString(CultureInfo.InvariantCulture),
                Framing.DefaultPerPageCount.ToString(CultureInfo.InvariantCulture),
                startDate ?? string.Empty,
                endDate ?? string.Empty);

            var frame = FrameCodec.EncodeFrame(MessageTypes.QueryMoneySupplyDataMonthRequest, body);
            await _transport.SendAsync(frame, ct).ConfigureAwait(false);

            var responseFrame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
            var (_, respBody) = DecodeResponseFrame(responseFrame);

            var page = ResponseParser.ParsePage(respBody);

            if (!string.Equals(page.ErrorCode, "0", StringComparison.Ordinal))
                throw new BaostockException(page.ErrorCode, page.ErrorMessage);

            foreach (var row in page.Rows)
            {
                yield return new MoneySupplyMonthRow(
                    StatYear: NullIfEmpty(row[0]),
                    StatMonth: NullIfEmpty(row[1]),
                    M0Month: NullIfEmpty(row[2]),
                    M0YOY: NullIfEmpty(row[3]),
                    M0ChainRelative: NullIfEmpty(row[4]),
                    M1Month: NullIfEmpty(row[5]),
                    M1YOY: NullIfEmpty(row[6]),
                    M1ChainRelative: NullIfEmpty(row[7]),
                    M2Month: NullIfEmpty(row[8]),
                    M2YOY: NullIfEmpty(row[9]),
                    M2ChainRelative: NullIfEmpty(row[10]));
            }

            if (page.Rows.Count < page.PerPageCount) break;
            curPage++;
        }
    }

    /// <summary>
    /// 查询货币供应量（年底余额）。MSG 55/56。
    /// </summary>
    /// <param name="startDate">开始日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="endDate">结束日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行货币供应量年度数据。</returns>
    public async IAsyncEnumerable<MoneySupplyYearRow> QueryMoneySupplyDataYearAsync(
        string? startDate = null,
        string? endDate = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureLoggedInAsync(ct).ConfigureAwait(false);

        var curPage = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var body = string.Join(Framing.MessageSplit,
                "query_money_supply_data_year",
                Session.UserId ?? "anonymous",
                curPage.ToString(CultureInfo.InvariantCulture),
                Framing.DefaultPerPageCount.ToString(CultureInfo.InvariantCulture),
                startDate ?? string.Empty,
                endDate ?? string.Empty);

            var frame = FrameCodec.EncodeFrame(MessageTypes.QueryMoneySupplyDataYearRequest, body);
            await _transport.SendAsync(frame, ct).ConfigureAwait(false);

            var responseFrame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
            var (_, respBody) = DecodeResponseFrame(responseFrame);

            var page = ResponseParser.ParsePage(respBody);

            if (!string.Equals(page.ErrorCode, "0", StringComparison.Ordinal))
                throw new BaostockException(page.ErrorCode, page.ErrorMessage);

            foreach (var row in page.Rows)
            {
                yield return new MoneySupplyYearRow(
                    StatYear: NullIfEmpty(row[0]),
                    M0Year: NullIfEmpty(row[1]),
                    M0YearYOY: NullIfEmpty(row[2]),
                    M1Year: NullIfEmpty(row[3]),
                    M1YearYOY: NullIfEmpty(row[4]),
                    M2Year: NullIfEmpty(row[5]),
                    M2YearYOY: NullIfEmpty(row[6]));
            }

            if (page.Rows.Count < page.PerPageCount) break;
            curPage++;
        }
    }
}
