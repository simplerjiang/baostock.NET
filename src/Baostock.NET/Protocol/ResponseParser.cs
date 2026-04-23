using System.Text.Json;

namespace Baostock.NET.Protocol;

/// <summary>
/// 解析单页查询响应 body（已解压/解码后的字符串）。
/// body 按 <c>\x01</c> 分段，前 N 项是元数据，其中第 6 项是 JSON <c>{"record":[...]}</c> 行数据。
/// 该解析器是通用的，v0.4.0+ 的其他 query 也复用此结构。
/// </summary>
public static class ResponseParser
{
    /// <summary>
    /// 解析单页响应 body。
    /// </summary>
    /// <param name="body">
    /// 经 <see cref="FrameCodec.DecodeFrame"/> 解码（非压缩）或 zlib 解压后的完整 body 字符串。
    /// 格式：<c>error_code\x01error_msg\x01method\x01user_id\x01cur_page_num\x01per_page_count\x01json_data\x01...\x01fields\x01...</c>
    /// </param>
    public static PageResult ParsePage(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var parts = body.Split(Framing.MessageSplit[0]);

        // 至少需要 error_code(0) + error_msg(1) 两段
        if (parts.Length < 2)
        {
            throw new FormatException($"响应 body 分段数不足（期望至少 2，实际 {parts.Length}）。");
        }

        var errorCode = parts[0];
        var errorMsg = parts[1];

        if (parts.Length < 9)
        {
            // 错误响应可能只有 error_code + error_msg
            return new PageResult(
                errorCode, errorMsg,
                Method: string.Empty,
                UserId: string.Empty,
                Fields: [],
                CurPageNum: 0,
                PerPageCount: 0,
                TotalPageNum: 0,
                Rows: []);
        }

        var method = parts[2];
        var userId = parts[3];

        int.TryParse(parts[4], System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var curPageNum);
        int.TryParse(parts[5], System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var perPageCount);

        // parts[6] = JSON data: {"record":[[...],[...],...]}
        var rows = ParseJsonRecords(parts[6]);

        // parts[8] = fields (comma-separated)
        var fields = parts.Length > 8
            ? parts[8].Split(Framing.AttributeSplit[0], StringSplitOptions.TrimEntries)
            : [];

        // 分页总页数：baostock 不直接返回 totalPageNum，
        // 如果当前页行数 < perPageCount，说明是最后一页。
        // 约定：当前页行数不足 perPageCount 时 totalPageNum = curPageNum，
        //       否则 totalPageNum = curPageNum + 1（乐观假设至少还有一页，调用方继续翻页直到行数不足）。
        var totalPageNum = (rows.Count < perPageCount) ? curPageNum : curPageNum + 1;

        return new PageResult(
            errorCode, errorMsg, method, userId,
            fields, curPageNum, perPageCount, totalPageNum, rows);
    }

    private static List<string[]> ParseJsonRecords(string jsonData)
    {
        if (string.IsNullOrWhiteSpace(jsonData))
        {
            return [];
        }

        // 上游 Python 先做 split() + "".join() 以去除换行等空白字符
        var cleaned = string.Concat(jsonData.Split(default(char[]), StringSplitOptions.RemoveEmptyEntries));

        using var doc = JsonDocument.Parse(cleaned);
        var root = doc.RootElement;

        if (!root.TryGetProperty("record", out var recordArray)
            || recordArray.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var rows = new List<string[]>(recordArray.GetArrayLength());
        foreach (var rowElement in recordArray.EnumerateArray())
        {
            if (rowElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var cols = new string[rowElement.GetArrayLength()];
            var i = 0;
            foreach (var cell in rowElement.EnumerateArray())
            {
                cols[i++] = cell.GetString() ?? string.Empty;
            }
            rows.Add(cols);
        }

        return rows;
    }
}

/// <summary>
/// 单页查询响应的结构化结果。
/// </summary>
/// <param name="ErrorCode">错误码（成功时为 <c>"0"</c>）。</param>
/// <param name="ErrorMessage">错误信息。</param>
/// <param name="Method">服务端回填的方法名。</param>
/// <param name="UserId">服务端回填的用户 ID。</param>
/// <param name="Fields">字段列表。</param>
/// <param name="CurPageNum">当前页码。</param>
/// <param name="PerPageCount">每页条数。</param>
/// <param name="TotalPageNum">总页数。</param>
/// <param name="Rows">数据行列表，每行是字符串数组。</param>
public sealed record PageResult(
    string ErrorCode,
    string ErrorMessage,
    string Method,
    string UserId,
    string[] Fields,
    int CurPageNum,
    int PerPageCount,
    int TotalPageNum,
    List<string[]> Rows);
