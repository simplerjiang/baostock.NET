using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Baostock.NET.Models;
using Baostock.NET.Protocol;

namespace Baostock.NET.Client;

public partial class BaostockClient
{
    private const string DefaultDailyFields =
        "date,code,open,high,low,close,preclose,volume,amount,adjustflag,turn,tradestatus,pctChg,isST";

    private const string DefaultMinuteFields = "date,time,code,open,high,low,close,volume,amount,adjustflag";

    /// <summary>
    /// 查询历史 K 线数据（日/周/月频）。自动按需分页拉取，流式 yield 每行。
    /// </summary>
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

        if (code.Length != Framing.StockCodeLength)
        {
            throw new ArgumentException(
                $"证券代码长度必须为 {Framing.StockCodeLength}（如 sh.600000），实际：'{code}'。",
                nameof(code));
        }

        code = code.ToLowerInvariant();
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

        if (code.Length != Framing.StockCodeLength)
        {
            throw new ArgumentException(
                $"证券代码长度必须为 {Framing.StockCodeLength}（如 sh.600000），实际：'{code}'。",
                nameof(code));
        }

        code = code.ToLowerInvariant();
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

    private static MinuteKLineRow ParseMinuteKLineRow(string[] cols)
    {
        // 字段顺序：date,time,code,open,high,low,close,volume,amount,adjustflag
        return new MinuteKLineRow(
            Date: DateOnly.ParseExact(cols[0], "yyyy-MM-dd", CultureInfo.InvariantCulture),
            Time: cols[1],
            Code: cols[2],
            Open: ParseNullableDecimal(cols[3]),
            High: ParseNullableDecimal(cols[4]),
            Low: ParseNullableDecimal(cols[5]),
            Close: ParseNullableDecimal(cols[6]),
            Volume: ParseNullableLong(cols[7]),
            Amount: ParseNullableDecimal(cols[8]),
            AdjustFlag: (AdjustFlag)int.Parse(cols[9], CultureInfo.InvariantCulture));
    }

    private static KLineRow ParseKLineRow(string[] cols)
    {
        // 字段顺序与 DefaultDailyFields 一致：
        // date,code,open,high,low,close,preclose,volume,amount,adjustflag,turn,tradestatus,pctChg,isST
        return new KLineRow(
            Date: DateOnly.ParseExact(cols[0], "yyyy-MM-dd", CultureInfo.InvariantCulture),
            Code: cols[1],
            Open: ParseNullableDecimal(cols[2]),
            High: ParseNullableDecimal(cols[3]),
            Low: ParseNullableDecimal(cols[4]),
            Close: ParseNullableDecimal(cols[5]),
            PreClose: ParseNullableDecimal(cols[6]),
            Volume: ParseNullableLong(cols[7]),
            Amount: ParseNullableDecimal(cols[8]),
            AdjustFlag: (AdjustFlag)int.Parse(cols[9], CultureInfo.InvariantCulture),
            Turn: ParseNullableDecimal(cols[10]),
            TradeStatus: (TradeStatus)int.Parse(cols[11], CultureInfo.InvariantCulture),
            PctChg: ParseNullableDecimal(cols[12]),
            IsST: cols[13] == "1");
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
