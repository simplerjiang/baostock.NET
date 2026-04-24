using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Baostock.NET.Models;
using Baostock.NET.Protocol;
using Baostock.NET.Util;

namespace Baostock.NET.Client;

public partial class BaostockClient
{
    private const string DefaultDailyFields =
        "date,code,open,high,low,close,preclose,volume,amount,adjustflag,turn,tradestatus,pctChg,isST";

    private const string DefaultMinuteFields = "date,time,code,open,high,low,close,volume,amount,adjustflag";

    /// <summary>
    /// 查询历史 K 线数据（日/周/月频）。自动按需分页拉取，流式 yield 每行。
    /// </summary>
    /// <param name="code">证券代码，东方财富风格 <c>"SH600000"</c> / <c>"SZ000001"</c> / <c>"BJ430047"</c>；亦兼容 <c>"sh.600000"</c> / <c>"sh600000"</c> / <c>"600000.SH"</c> 等格式。</param>
    /// <param name="fields">查询字段，逗号分隔；为 <c>null</c> 时使用默认日频字段。</param>
    /// <param name="startDate">开始日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="endDate">结束日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="frequency">K 线频率（仅支持日/周/月）。</param>
    /// <param name="adjustFlag">复权类型。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行 K 线数据。</returns>
    public async IAsyncEnumerable<KLineRow> QueryHistoryKDataPlusAsync(
        string code,
        string? fields = null,
        string? startDate = null,
        string? endDate = null,
        KLineFrequency frequency = KLineFrequency.Day,
        AdjustFlag adjustFlag = AdjustFlag.PreAdjust,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureLoggedInAsync(ct).ConfigureAwait(false);

        ArgumentException.ThrowIfNullOrEmpty(code);
        if (frequency is KLineFrequency.FiveMinute or KLineFrequency.FifteenMinute
            or KLineFrequency.ThirtyMinute or KLineFrequency.SixtyMinute)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frequency), frequency,
                "请使用 QueryHistoryKDataPlusMinuteAsync 查询分钟级数据");
        }

        // v1.2.0 BREAKING：对外接受东财风格（SH600000），内部翻译为 baostock 协议格式（sh.600000）。
        code = CodeFormatter.ToBaostock(code);
        fields ??= DefaultDailyFields;
        startDate ??= "2017-07-01"; // DEFAULT_START_DATE from contants.py
        endDate ??= DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var freqStr = FrequencyToString(frequency);
        var adjStr = ((int)adjustFlag).ToString(CultureInfo.InvariantCulture);

        var curPage = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var body = string.Join(Framing.MessageSplit,
                "query_history_k_data_plus",
                Session.UserId ?? "anonymous",
                curPage.ToString(CultureInfo.InvariantCulture),
                Framing.DefaultPerPageCount.ToString(CultureInfo.InvariantCulture),
                code,
                fields,
                startDate,
                endDate,
                freqStr,
                adjStr);

            var frame = FrameCodec.EncodeFrame(MessageTypes.GetKDataPlusRequest, body);
            await _transport.SendAsync(frame, ct).ConfigureAwait(false);

            var responseFrame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
            var (header, respBody) = DecodeResponseFrame(responseFrame, header =>
                string.Equals(header.MessageType, MessageTypes.GetKDataPlusResponse, StringComparison.Ordinal));

            var page = ResponseParser.ParsePage(respBody);

            if (!string.Equals(page.ErrorCode, "0", StringComparison.Ordinal))
            {
                throw new BaostockException(page.ErrorCode, page.ErrorMessage);
            }

            foreach (var row in page.Rows)
            {
                yield return ParseKLineRow(row);
            }

            // 如果本页行数不足 perPageCount，没有更多页了
            if (page.Rows.Count < page.PerPageCount)
            {
                break;
            }

            curPage++;
        }
    }

    /// <summary>
    /// 解码响应帧：处理 MSG=04 异常帧、压缩帧解压、非压缩帧解码。
    /// </summary>
    private (MessageHeader Header, string Body) DecodeResponseFrame(
        byte[] responseFrame, Func<MessageHeader, bool>? isCompressed = null)
    {
        var header = MessageHeader.Parse(responseFrame.AsSpan(0, Framing.MessageHeaderLength));

        if (header.MessageType == MessageTypes.Exception)
        {
            var exBody = FrameCodec.DecodeFrame(responseFrame).Body;
            ThrowFromExceptionFrame(exBody);
        }

        if (isCompressed?.Invoke(header) == true)
        {
            // 压缩帧：body 从 header 后开始，长度由 header.BodyLength 权威确定。
            // 不做 CRC 校验——上游 Python 客户端也不校验响应 CRC，且 zlib 自带 adler32 完整性检查。
            var compressedBody = responseFrame.AsSpan(
                Framing.MessageHeaderLength, header.BodyLength);
            var decompressed = FrameCodec.Decompress(compressedBody);
            var bodyStr = Encoding.UTF8.GetString(decompressed);
            return (header, bodyStr);
        }

        return FrameCodec.DecodeFrame(responseFrame);
    }

    /// <summary>
    /// 查询历史 K 线数据（分钟频：5/15/30/60 分钟）。自动按需分页拉取，流式 yield 每行。
    /// </summary>
    /// <param name="code">证券代码，东方财富风格 <c>"SH600000"</c> / <c>"SZ000001"</c> / <c>"BJ430047"</c>；亦兼容 <c>"sh.600000"</c> / <c>"sh600000"</c> / <c>"600000.SH"</c> 等格式。</param>
    /// <param name="fields">查询字段，逗号分隔；为 <c>null</c> 时使用默认分钟频字段。</param>
    /// <param name="startDate">开始日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="endDate">结束日期，格式 <c>"yyyy-MM-dd"</c>。</param>
    /// <param name="frequency">K 线频率（仅支持 5/15/30/60 分钟）。</param>
    /// <param name="adjustFlag">复权类型。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流式返回每行分钟 K 线数据。</returns>
    public async IAsyncEnumerable<MinuteKLineRow> QueryHistoryKDataPlusMinuteAsync(
        string code,
        string? fields = null,
        string? startDate = null,
        string? endDate = null,
        KLineFrequency frequency = KLineFrequency.FiveMinute,
        AdjustFlag adjustFlag = AdjustFlag.PreAdjust,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureLoggedInAsync(ct).ConfigureAwait(false);

        ArgumentException.ThrowIfNullOrEmpty(code);
        if (frequency is not (KLineFrequency.FiveMinute or KLineFrequency.FifteenMinute
            or KLineFrequency.ThirtyMinute or KLineFrequency.SixtyMinute))
        {
            throw new ArgumentOutOfRangeException(
                nameof(frequency), frequency,
                "分钟频方法仅支持 FiveMinute/FifteenMinute/ThirtyMinute/SixtyMinute");
        }

        // v1.2.0 BREAKING：对外接受东财风格（SH600000），内部翻译为 baostock 协议格式（sh.600000）。
        code = CodeFormatter.ToBaostock(code);
        fields ??= DefaultMinuteFields;
        startDate ??= "2017-07-01";
        endDate ??= DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var freqStr = FrequencyToString(frequency);
        var adjStr = ((int)adjustFlag).ToString(CultureInfo.InvariantCulture);

        var curPage = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var body = string.Join(Framing.MessageSplit,
                "query_history_k_data_plus",
                Session.UserId ?? "anonymous",
                curPage.ToString(CultureInfo.InvariantCulture),
                Framing.DefaultPerPageCount.ToString(CultureInfo.InvariantCulture),
                code,
                fields,
                startDate,
                endDate,
                freqStr,
                adjStr);

            var frame = FrameCodec.EncodeFrame(MessageTypes.GetKDataPlusRequest, body);
            await _transport.SendAsync(frame, ct).ConfigureAwait(false);

            var responseFrame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
            var (header, respBody) = DecodeResponseFrame(responseFrame, header =>
                string.Equals(header.MessageType, MessageTypes.GetKDataPlusResponse, StringComparison.Ordinal));

            var page = ResponseParser.ParsePage(respBody);

            if (!string.Equals(page.ErrorCode, "0", StringComparison.Ordinal))
            {
                throw new BaostockException(page.ErrorCode, page.ErrorMessage);
            }

            foreach (var row in page.Rows)
            {
                yield return ParseMinuteKLineRow(row);
            }

            if (page.Rows.Count < page.PerPageCount)
            {
                break;
            }

            curPage++;
        }
    }

    internal static string SafeCol(string[] cols, int i)
        => i < cols.Length ? cols[i] : string.Empty;

    internal static MinuteKLineRow ParseMinuteKLineRow(string[] cols)
    {
        // 字段顺序：date,time,code,open,high,low,close,volume,amount,adjustflag
        return new MinuteKLineRow(
            Date: DateOnly.ParseExact(SafeCol(cols, 0), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            Time: SafeCol(cols, 1),
            Code: FormatModelCode(SafeCol(cols, 2)),
            Open: ParseNullableDecimal(SafeCol(cols, 3)),
            High: ParseNullableDecimal(SafeCol(cols, 4)),
            Low: ParseNullableDecimal(SafeCol(cols, 5)),
            Close: ParseNullableDecimal(SafeCol(cols, 6)),
            Volume: ParseNullableLong(SafeCol(cols, 7)),
            Amount: ParseNullableDecimal(SafeCol(cols, 8)),
            AdjustFlag: SafeCol(cols, 9) is { Length: > 0 } af ? (AdjustFlag)int.Parse(af, CultureInfo.InvariantCulture) : AdjustFlag.NoAdjust);
    }

    internal static KLineRow ParseKLineRow(string[] cols)
    {
        // 字段顺序与 DefaultDailyFields 一致：
        // date,code,open,high,low,close,preclose,volume,amount,adjustflag,turn,tradestatus,pctChg,isST
        return new KLineRow(
            Date: DateOnly.ParseExact(SafeCol(cols, 0), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            Code: FormatModelCode(SafeCol(cols, 1)),
            Open: ParseNullableDecimal(SafeCol(cols, 2)),
            High: ParseNullableDecimal(SafeCol(cols, 3)),
            Low: ParseNullableDecimal(SafeCol(cols, 4)),
            Close: ParseNullableDecimal(SafeCol(cols, 5)),
            PreClose: ParseNullableDecimal(SafeCol(cols, 6)),
            Volume: ParseNullableLong(SafeCol(cols, 7)),
            Amount: ParseNullableDecimal(SafeCol(cols, 8)),
            AdjustFlag: SafeCol(cols, 9) is { Length: > 0 } af ? (AdjustFlag)int.Parse(af, CultureInfo.InvariantCulture) : AdjustFlag.NoAdjust,
            Turn: ParseNullableDecimal(SafeCol(cols, 10)),
            TradeStatus: SafeCol(cols, 11) is { Length: > 0 } ts ? (TradeStatus)int.Parse(ts, CultureInfo.InvariantCulture) : TradeStatus.Suspended,
            PctChg: ParseNullableDecimal(SafeCol(cols, 12)),
            IsST: SafeCol(cols, 13) == "1");
    }

    private static decimal? ParseNullableDecimal(string value)
        => string.IsNullOrEmpty(value) ? null : decimal.Parse(value, CultureInfo.InvariantCulture);

    private static long? ParseNullableLong(string value)
        => string.IsNullOrEmpty(value) ? null : long.Parse(value, CultureInfo.InvariantCulture);

    private static string FrequencyToString(KLineFrequency freq) => freq switch
    {
        KLineFrequency.Day => "d",
        KLineFrequency.Week => "w",
        KLineFrequency.Month => "m",
        KLineFrequency.FiveMinute => "5",
        KLineFrequency.FifteenMinute => "15",
        KLineFrequency.ThirtyMinute => "30",
        KLineFrequency.SixtyMinute => "60",
        _ => throw new ArgumentOutOfRangeException(nameof(freq), freq, null)
    };
}
