using System.Globalization;
using System.Text.Json;
using Baostock.NET.Client;
using Baostock.NET.Cninfo;
using Baostock.NET.Financials;
using Baostock.NET.Models;

namespace Baostock.NET.TestUI.Endpoints;

/// <summary>
/// 一个可路由的 endpoint：metadata + 实际 handler。
/// handler 入参是请求 body（JsonElement）+ client + ct，返回 (rowCount, data)。
/// </summary>
/// <param name="SourcesExtractor">
/// 可选：HTTP 多源对冲端点提供的"从 data 抽取实际响应源名称"的回调。
/// 由 <see cref="EndpointRunner"/> 在成功路径调用，结果写入 <see cref="ApiResult.Sources"/>。
/// 单源（TCP / cninfo）端点可硬编码常量列表；多源（财报）端点可用反射读 row.Source。
/// </param>
public sealed record RoutedEndpoint(
    EndpointDescriptor Meta,
    Func<JsonElement, BaostockClient, CancellationToken, Task<(int rowCount, object? data)>> Handler,
    Func<object?, IReadOnlyList<string>?>? SourcesExtractor = null);

/// <summary>
/// 集中维护全部 baostock TCP API（28 个）+ 多源 API（3 个）的元数据与 handler。
/// 不依赖反射，避免泄露 internal 噪音。
/// </summary>
public static class EndpointRegistry
{
    private static readonly string[] FrequencyOptions =
        Enum.GetNames<KLineFrequency>();

    private static readonly string[] AdjustOptions =
        Enum.GetNames<AdjustFlag>();

    private static readonly string[] MinuteFrequencyOptions =
    {
        nameof(KLineFrequency.FiveMinute),
        nameof(KLineFrequency.FifteenMinute),
        nameof(KLineFrequency.ThirtyMinute),
        nameof(KLineFrequency.SixtyMinute),
    };

    private static readonly string[] DailyFrequencyOptions =
    {
        nameof(KLineFrequency.Day),
        nameof(KLineFrequency.Week),
        nameof(KLineFrequency.Month),
    };

    // ── 动态默认值 ───────────────────────────────────
    // 这些 helper 在每次 BuildAll() 调用时（即每次 GET /api/meta/endpoints）求值，
    // 保证进程跨日运行后 metadata 中的默认日期/季度会跟着自然日滚动。
    // 严禁把它们的结果缓存到 static readonly 字段。
    private static string Today() =>
        DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static string DaysAgo(int days) =>
        DateTime.Today.AddDays(-days).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static string CurrentYear() =>
        DateTime.Today.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>上一个完整年度（=今年-1）。N-1：dividend-data 默认值，避免落到本年导致 0 行。</summary>
    private static string LastCompletedYear() =>
        (DateTime.Today.Year - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>取上一个完整季度（1..4），<c>Math.Max(1, (Month-1)/3)</c>。</summary>
    private static string LastCompletedQuarter() =>
        Math.Max(1, (DateTime.Today.Month - 1) / 3)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>构建全部 endpoint。顺序即前端展示顺序。</summary>
    public static IReadOnlyList<RoutedEndpoint> BuildAll()
    {
        var list = new List<RoutedEndpoint>();
        list.AddRange(BuildHistory());
        list.AddRange(BuildMetadata());
        list.AddRange(BuildSector());
        list.AddRange(BuildEvaluation());
        list.AddRange(BuildCorp());
        list.AddRange(BuildMacro());
        list.AddRange(BuildSpecial());
        list.AddRange(BuildMulti());
        list.AddRange(BuildFinancial());
        list.AddRange(BuildCninfo());
        return list;
    }

    /// <summary>
    /// 构建 TestUI 自身管理的"内部"端点 metadata（session/meta/loadtest），
    /// 仅供 <c>/api/meta/endpoints</c> 暴露给前端展示，不参与 <see cref="BuildAll"/> 路由注册
    /// （这些路径已在 <c>Program.cs</c> 用 MapGet/MapPost 直接挂上）。
    /// 全部标记 <c>protocol="internal"</c>，前端据此渲染 <c>[META]</c> 徽章并禁用压测按钮。
    /// 新增于 v1.2.0-preview5（N-3：把原 dead code 分支变活）。
    /// </summary>
    public static IReadOnlyList<EndpointDescriptor> BuildInternalMeta()
    {
        return new[]
        {
            new EndpointDescriptor("session", "Login",
                "/api/session/login",
                "登录 baostock 会话（默认匿名 anonymous/123456）",
                new[]
                {
                    new FieldDescriptor("userId", "string", false, "anonymous"),
                    new FieldDescriptor("password", "string", false, "123456"),
                },
                Protocol: "internal"),
            new EndpointDescriptor("session", "Logout",
                "/api/session/logout",
                "登出当前会话",
                Array.Empty<FieldDescriptor>(),
                Protocol: "internal"),
            new EndpointDescriptor("session", "Status",
                "/api/session/status",
                "查询会话状态：含 isLoggedIn / isSocketConnected（v1.2.0-preview5 起）",
                Array.Empty<FieldDescriptor>(),
                Protocol: "internal"),
            new EndpointDescriptor("meta", "Endpoints",
                "/api/meta/endpoints",
                "列出全部 endpoint 元数据（驱动前端表单自动渲染）",
                Array.Empty<FieldDescriptor>(),
                Protocol: "internal"),
            new EndpointDescriptor("loadtest", "ListTargets",
                "/api/loadtest/list-targets",
                "列出可压测的 endpoint（自动排除 internal/loadtest/meta）",
                Array.Empty<FieldDescriptor>(),
                Protocol: "internal"),
            new EndpointDescriptor("loadtest", "Run",
                "/api/loadtest/run",
                "运行进程内压测（concurrency / mode=duration|count / warmup）",
                Array.Empty<FieldDescriptor>(),
                Protocol: "internal"),
        };
    }

    // ── history (2) ──────────────────────────────────
    private static IEnumerable<RoutedEndpoint> BuildHistory()
    {
        yield return new RoutedEndpoint(
            new EndpointDescriptor("history", "QueryHistoryKDataPlusAsync",
                "/api/baostock/history/k-data-plus",
                "查询历史 K 线（日/周/月频）",
                new[]
                {
                    new FieldDescriptor("code", "string", true, "SH600519"),
                    new FieldDescriptor("fields", "string", false, "date,code,open,high,low,close,preclose,volume,amount,adjustflag,turn,tradestatus,pctChg,isST"),
                    new FieldDescriptor("startDate", "string", false, DaysAgo(30)),
                    new FieldDescriptor("endDate", "string", false, Today()),
                    new FieldDescriptor("frequency", "enum", false, "Day", DailyFrequencyOptions),
                    new FieldDescriptor("adjustFlag", "enum", false, "PreAdjust", AdjustOptions),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(c.QueryHistoryKDataPlusAsync(
                EndpointRunner.GetString(body, "code") ?? "SH600519",
                EndpointRunner.GetString(body, "fields"),
                EndpointRunner.GetString(body, "startDate"),
                EndpointRunner.GetString(body, "endDate"),
                EndpointRunner.GetEnum(body, "frequency", KLineFrequency.Day),
                EndpointRunner.GetEnum(body, "adjustFlag", AdjustFlag.PreAdjust),
                ct), ct));

        yield return new RoutedEndpoint(
            new EndpointDescriptor("history", "QueryHistoryKDataPlusMinuteAsync",
                "/api/baostock/history/k-data-plus-minute",
                "查询历史 K 线（5/15/30/60 分钟频）",
                new[]
                {
                    new FieldDescriptor("code", "string", true, "SH600519"),
                    new FieldDescriptor("fields", "string", false, "date,time,code,open,high,low,close,volume,amount,adjustflag"),
                    new FieldDescriptor("startDate", "string", false, DaysAgo(30)),
                    new FieldDescriptor("endDate", "string", false, Today()),
                    new FieldDescriptor("frequency", "enum", false, "FiveMinute", MinuteFrequencyOptions),
                    new FieldDescriptor("adjustFlag", "enum", false, "PreAdjust", AdjustOptions),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(c.QueryHistoryKDataPlusMinuteAsync(
                EndpointRunner.GetString(body, "code") ?? "SH600519",
                EndpointRunner.GetString(body, "fields"),
                EndpointRunner.GetString(body, "startDate"),
                EndpointRunner.GetString(body, "endDate"),
                EndpointRunner.GetEnum(body, "frequency", KLineFrequency.FiveMinute),
                EndpointRunner.GetEnum(body, "adjustFlag", AdjustFlag.PreAdjust),
                ct), ct));
    }

    // ── metadata (3) ─────────────────────────────────
    private static IEnumerable<RoutedEndpoint> BuildMetadata()
    {
        yield return new RoutedEndpoint(
            new EndpointDescriptor("metadata", "QueryTradeDatesAsync",
                "/api/baostock/metadata/trade-dates",
                "查询交易日信息",
                new[]
                {
                    new FieldDescriptor("startDate", "string", false, DaysAgo(30)),
                    new FieldDescriptor("endDate", "string", false, Today()),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(c.QueryTradeDatesAsync(
                EndpointRunner.GetString(body, "startDate"),
                EndpointRunner.GetString(body, "endDate"),
                ct), ct));

        yield return new RoutedEndpoint(
            new EndpointDescriptor("metadata", "QueryAllStockAsync",
                "/api/baostock/metadata/all-stock",
                "查询指定日期的全部证券列表",
                new[]
                {
                    new FieldDescriptor("day", "string", false, Today()),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(c.QueryAllStockAsync(
                EndpointRunner.GetString(body, "day"),
                ct), ct));

        yield return new RoutedEndpoint(
            new EndpointDescriptor("metadata", "QueryStockBasicAsync",
                "/api/baostock/metadata/stock-basic",
                "查询证券基本资料（code 与 codeName 均可为空，codeName 模糊匹配）",
                new[]
                {
                    new FieldDescriptor("code", "string", false, "SH600519"),
                    new FieldDescriptor("codeName", "string", false, ""),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(c.QueryStockBasicAsync(
                EndpointRunner.GetString(body, "code"),
                EndpointRunner.GetString(body, "codeName"),
                ct), ct));
    }

    // ── sector (4) ───────────────────────────────────
    private static IEnumerable<RoutedEndpoint> BuildSector()
    {
        yield return new RoutedEndpoint(
            new EndpointDescriptor("sector", "QueryStockIndustryAsync",
                "/api/baostock/sector/stock-industry",
                "查询行业分类",
                new[]
                {
                    new FieldDescriptor("code", "string", false, "SH600519"),
                    new FieldDescriptor("date", "string", false, ""),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(c.QueryStockIndustryAsync(
                EndpointRunner.GetString(body, "code"),
                EndpointRunner.GetString(body, "date"),
                ct), ct));

        yield return new RoutedEndpoint(
            new EndpointDescriptor("sector", "QueryHs300StocksAsync",
                "/api/baostock/sector/hs300-constituent",
                "查询沪深 300 成分股",
                new[]
                {
                    new FieldDescriptor("date", "string", false, ""),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(c.QueryHs300StocksAsync(
                EndpointRunner.GetString(body, "date"), ct), ct));

        yield return new RoutedEndpoint(
            new EndpointDescriptor("sector", "QuerySz50StocksAsync",
                "/api/baostock/sector/sz50-constituent",
                "查询上证 50 成分股",
                new[]
                {
                    new FieldDescriptor("date", "string", false, ""),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(c.QuerySz50StocksAsync(
                EndpointRunner.GetString(body, "date"), ct), ct));

        yield return new RoutedEndpoint(
            new EndpointDescriptor("sector", "QueryZz500StocksAsync",
                "/api/baostock/sector/zz500-constituent",
                "查询中证 500 成分股",
                new[]
                {
                    new FieldDescriptor("date", "string", false, ""),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(c.QueryZz500StocksAsync(
                EndpointRunner.GetString(body, "date"), ct), ct));
    }

    // ── evaluation (8) ────────────────────────────────
    private static IEnumerable<RoutedEndpoint> BuildEvaluation()
    {
        yield return new RoutedEndpoint(
            new EndpointDescriptor("evaluation", "QueryDividendDataAsync",
                "/api/baostock/evaluation/dividend-data",
                "查询股息分红",
                new[]
                {
                    new FieldDescriptor("code", "string", true, "SH600519"),
                    new FieldDescriptor("year", "string", false, LastCompletedYear()),
                    new FieldDescriptor("yearType", "enum", false, "report", new[] { "report", "operate" }),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(c.QueryDividendDataAsync(
                EndpointRunner.GetString(body, "code") ?? "SH600519",
                EndpointRunner.GetString(body, "year"),
                EndpointRunner.GetString(body, "yearType") ?? "report",
                ct), ct));

        yield return new RoutedEndpoint(
            new EndpointDescriptor("evaluation", "QueryAdjustFactorAsync",
                "/api/baostock/evaluation/adjust-factor",
                "查询复权因子",
                new[]
                {
                    new FieldDescriptor("code", "string", true, "SH600519"),
                    new FieldDescriptor("startDate", "string", false, DaysAgo(30)),
                    new FieldDescriptor("endDate", "string", false, Today()),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(c.QueryAdjustFactorAsync(
                EndpointRunner.GetString(body, "code") ?? "SH600519",
                EndpointRunner.GetString(body, "startDate"),
                EndpointRunner.GetString(body, "endDate"),
                ct), ct));

        yield return BuildSeasonEndpoint("QueryProfitDataAsync",
            "/api/baostock/evaluation/profit-data", "查询季频盈利能力",
            (c, code, year, quarter, ct) => c.QueryProfitDataAsync(code, year, quarter, ct));

        yield return BuildSeasonEndpoint("QueryOperationDataAsync",
            "/api/baostock/evaluation/operation-data", "查询季频营运能力",
            (c, code, year, quarter, ct) => c.QueryOperationDataAsync(code, year, quarter, ct));

        yield return BuildSeasonEndpoint("QueryGrowthDataAsync",
            "/api/baostock/evaluation/growth-data", "查询季频成长能力",
            (c, code, year, quarter, ct) => c.QueryGrowthDataAsync(code, year, quarter, ct));

        yield return BuildSeasonEndpoint("QueryDupontDataAsync",
            "/api/baostock/evaluation/dupont-data", "查询季频杜邦指数",
            (c, code, year, quarter, ct) => c.QueryDupontDataAsync(code, year, quarter, ct));

        yield return BuildSeasonEndpoint("QueryBalanceDataAsync",
            "/api/baostock/evaluation/balance-data", "查询季频偿债能力",
            (c, code, year, quarter, ct) => c.QueryBalanceDataAsync(code, year, quarter, ct));

        yield return BuildSeasonEndpoint("QueryCashFlowDataAsync",
            "/api/baostock/evaluation/cash-flow-data", "查询季频现金流量",
            (c, code, year, quarter, ct) => c.QueryCashFlowDataAsync(code, year, quarter, ct));
    }

    private static RoutedEndpoint BuildSeasonEndpoint<T>(
        string name, string path, string desc,
        Func<BaostockClient, string, int, int, CancellationToken, IAsyncEnumerable<T>> invoke)
    {
        return new RoutedEndpoint(
            new EndpointDescriptor("evaluation", name, path, desc,
                new[]
                {
                    new FieldDescriptor("code", "string", true, "SH600519"),
                    new FieldDescriptor("year", "int", true, CurrentYear()),
                    new FieldDescriptor("quarter", "int", true, LastCompletedQuarter()),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(invoke(
                c,
                EndpointRunner.GetString(body, "code") ?? "SH600519",
                EndpointRunner.GetInt(body, "year", DateTime.Today.Year),
                EndpointRunner.GetInt(body, "quarter", Math.Max(1, (DateTime.Today.Month - 1) / 3)),
                ct), ct));
    }

    // ── corp (2) ─────────────────────────────────────
    private static IEnumerable<RoutedEndpoint> BuildCorp()
    {
        yield return new RoutedEndpoint(
            new EndpointDescriptor("corp", "QueryPerformanceExpressReportAsync",
                "/api/baostock/corp/performance-express-report",
                "查询公司业绩快报",
                new[]
                {
                    new FieldDescriptor("code", "string", true, "SH600519"),
                    new FieldDescriptor("startDate", "string", false, DaysAgo(30)),
                    new FieldDescriptor("endDate", "string", false, Today()),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(c.QueryPerformanceExpressReportAsync(
                EndpointRunner.GetString(body, "code") ?? "SH600519",
                EndpointRunner.GetString(body, "startDate"),
                EndpointRunner.GetString(body, "endDate"),
                ct), ct));

        yield return new RoutedEndpoint(
            new EndpointDescriptor("corp", "QueryForecastReportAsync",
                "/api/baostock/corp/forecast-report",
                "查询公司业绩预告",
                new[]
                {
                    new FieldDescriptor("code", "string", true, "SH600519"),
                    new FieldDescriptor("startDate", "string", false, DaysAgo(30)),
                    new FieldDescriptor("endDate", "string", false, Today()),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(c.QueryForecastReportAsync(
                EndpointRunner.GetString(body, "code") ?? "SH600519",
                EndpointRunner.GetString(body, "startDate"),
                EndpointRunner.GetString(body, "endDate"),
                ct), ct));
    }

    // ── macro (5) ─────────────────────────────────────
    private static IEnumerable<RoutedEndpoint> BuildMacro()
    {
        yield return new RoutedEndpoint(
            new EndpointDescriptor("macro", "QueryDepositRateDataAsync",
                "/api/baostock/macro/deposit-rate",
                "查询存款利率",
                new[]
                {
                    new FieldDescriptor("startDate", "string", false, ""),
                    new FieldDescriptor("endDate", "string", false, ""),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(c.QueryDepositRateDataAsync(
                EndpointRunner.GetString(body, "startDate"),
                EndpointRunner.GetString(body, "endDate"),
                ct), ct));

        yield return new RoutedEndpoint(
            new EndpointDescriptor("macro", "QueryLoanRateDataAsync",
                "/api/baostock/macro/loan-rate",
                "查询贷款利率",
                new[]
                {
                    new FieldDescriptor("startDate", "string", false, ""),
                    new FieldDescriptor("endDate", "string", false, ""),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(c.QueryLoanRateDataAsync(
                EndpointRunner.GetString(body, "startDate"),
                EndpointRunner.GetString(body, "endDate"),
                ct), ct));

        yield return new RoutedEndpoint(
            new EndpointDescriptor("macro", "QueryRequiredReserveRatioDataAsync",
                "/api/baostock/macro/required-reserve-ratio",
                "查询存款准备金率",
                new[]
                {
                    new FieldDescriptor("startDate", "string", false, ""),
                    new FieldDescriptor("endDate", "string", false, ""),
                    new FieldDescriptor("yearType", "string", false, "0"),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(c.QueryRequiredReserveRatioDataAsync(
                EndpointRunner.GetString(body, "startDate"),
                EndpointRunner.GetString(body, "endDate"),
                EndpointRunner.GetString(body, "yearType") ?? "0",
                ct), ct));

        yield return new RoutedEndpoint(
            new EndpointDescriptor("macro", "QueryMoneySupplyDataMonthAsync",
                "/api/baostock/macro/money-supply-month",
                "查询货币供应量（月度）",
                new[]
                {
                    new FieldDescriptor("startDate", "string", false, ""),
                    new FieldDescriptor("endDate", "string", false, ""),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(c.QueryMoneySupplyDataMonthAsync(
                EndpointRunner.GetString(body, "startDate"),
                EndpointRunner.GetString(body, "endDate"),
                ct), ct));

        yield return new RoutedEndpoint(
            new EndpointDescriptor("macro", "QueryMoneySupplyDataYearAsync",
                "/api/baostock/macro/money-supply-year",
                "查询货币供应量（年度）",
                new[]
                {
                    new FieldDescriptor("startDate", "string", false, ""),
                    new FieldDescriptor("endDate", "string", false, ""),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(c.QueryMoneySupplyDataYearAsync(
                EndpointRunner.GetString(body, "startDate"),
                EndpointRunner.GetString(body, "endDate"),
                ct), ct));
    }

    // ── special (4) ──────────────────────────────────
    private static IEnumerable<RoutedEndpoint> BuildSpecial()
    {
        yield return BuildSpecialEndpoint("QueryTerminatedStocksAsync",
            "/api/baostock/special/terminated-stocks", "查询终止上市股票",
            (c, date, ct) => c.QueryTerminatedStocksAsync(date, ct));
        yield return BuildSpecialEndpoint("QuerySuspendedStocksAsync",
            "/api/baostock/special/suspended-stocks", "查询暂停上市股票",
            (c, date, ct) => c.QuerySuspendedStocksAsync(date, ct));
        yield return BuildSpecialEndpoint("QueryStStocksAsync",
            "/api/baostock/special/st-stocks", "查询 ST 股票",
            (c, date, ct) => c.QueryStStocksAsync(date, ct));
        yield return BuildSpecialEndpoint("QueryStarStStocksAsync",
            "/api/baostock/special/star-st-stocks", "查询 *ST 股票",
            (c, date, ct) => c.QueryStarStStocksAsync(date, ct));
    }

    private static RoutedEndpoint BuildSpecialEndpoint(
        string name, string path, string desc,
        Func<BaostockClient, string?, CancellationToken, IAsyncEnumerable<SpecialStockRow>> invoke)
    {
        return new RoutedEndpoint(
            new EndpointDescriptor("special", name, path, desc,
                new[]
                {
                    new FieldDescriptor("date", "string", false, ""),
                }),
            (body, c, ct) => EndpointRunner.DrainAsync(invoke(
                c, EndpointRunner.GetString(body, "date"), ct), ct));
    }

    // ── multi-source (3) ──────────────────────────────
    private static IEnumerable<RoutedEndpoint> BuildMulti()
    {
        yield return new RoutedEndpoint(
            new EndpointDescriptor("multi", "GetRealtimeQuoteAsync",
                "/api/multi/realtime-quote",
                "实时行情（单只，三源对冲：Sina → Tencent → EastMoney）",
                new[]
                {
                    new FieldDescriptor("code", "string", true, "SH600519"),
                },
                Protocol: "http"),
            async (body, c, ct) =>
            {
                var quote = await c.GetRealtimeQuoteAsync(
                    EndpointRunner.GetString(body, "code") ?? "SH600519", ct).ConfigureAwait(false);
                return (1, (object?)quote);
            });

        yield return new RoutedEndpoint(
            new EndpointDescriptor("multi", "GetRealtimeQuotesAsync",
                "/api/multi/realtime-quotes",
                "实时行情（批量，三源对冲）",
                new[]
                {
                    new FieldDescriptor("codes", "string[]", true, "SH600519,SZ000001"),
                },
                Protocol: "http"),
            async (body, c, ct) =>
            {
                var codes = EndpointRunner.GetStringArray(body, "codes");
                // M1 修复：空/缺失的 codes 必须报错，不允许静默回退到内置默认股票。
                // 丢出 ArgumentException 会被 EndpointRunner.RunAsync 换为 ApiResult { ok=false, error=...}。
                if (codes.Length == 0)
                {
                    throw new ArgumentException("codes is required and must be non-empty", nameof(codes));
                }
                var quotes = await c.GetRealtimeQuotesAsync(codes, ct).ConfigureAwait(false);
                return (quotes.Count, (object?)quotes);
            });

        yield return new RoutedEndpoint(
            new EndpointDescriptor("multi", "GetHistoryKLineAsync",
                "/api/multi/history-k-line",
                "历史 K 线（双源对冲：EastMoney → Tencent）",
                new[]
                {
                    new FieldDescriptor("code", "string", true, "SH600519"),
                    new FieldDescriptor("frequency", "enum", false, "Day", FrequencyOptions),
                    new FieldDescriptor("startDate", "string", true, DaysAgo(30)),
                    new FieldDescriptor("endDate", "string", true, Today()),
                    new FieldDescriptor("adjust", "enum", false, "PreAdjust", AdjustOptions),
                },
                Protocol: "http"),
            async (body, c, ct) =>
            {
                var code = EndpointRunner.GetString(body, "code") ?? "SH600519";
                var frequency = EndpointRunner.GetEnum(body, "frequency", KLineFrequency.Day);
                var adjust = EndpointRunner.GetEnum(body, "adjust", AdjustFlag.PreAdjust);
                var start = ParseDate(EndpointRunner.GetString(body, "startDate"), DateTime.Today.AddDays(-30));
                var end = ParseDate(EndpointRunner.GetString(body, "endDate"), DateTime.Today);
                var rows = await c.GetHistoryKLineAsync(code, frequency, start, end, adjust, ct).ConfigureAwait(false);
                return (rows.Count, (object?)rows);
            });
    }

    private static DateTime ParseDate(string? s, DateTime fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        return DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeLocal, out var dt) ? dt : fallback;
    }

    // ── financial (3) ─────────────────────────────────
    // v1.3.0 Sprint 3：财报三表 HTTP 多源对冲（东财 P=0 + 新浪 P=1，hedge 500ms）。
    // 绕过 baostock TCP 单连接瓶颈，可安全并发压测（Protocol="http"）。
    private static IEnumerable<RoutedEndpoint> BuildFinancial()
    {
        yield return BuildFinancialEndpoint("QueryFullBalanceSheetAsync",
            "/api/financial/balance-sheet", "完整资产负债表（东财 P=0 + 新浪 P=1 双源对冲）",
            (c, req, ct) => c.QueryFullBalanceSheetAsync(req, ct));

        yield return BuildFinancialEndpoint("QueryFullIncomeStatementAsync",
            "/api/financial/income-statement", "完整利润表（东财 P=0 + 新浪 P=1 双源对冲）",
            (c, req, ct) => c.QueryFullIncomeStatementAsync(req, ct));

        yield return BuildFinancialEndpoint("QueryFullCashFlowAsync",
            "/api/financial/cashflow", "完整现金流量表（东财 P=0 + 新浪 P=1 双源对冲）",
            (c, req, ct) => c.QueryFullCashFlowAsync(req, ct));
    }

    private static RoutedEndpoint BuildFinancialEndpoint<TRow>(
        string name, string path, string desc,
        Func<BaostockClient, FinancialStatementRequest, CancellationToken, Task<IReadOnlyList<TRow>>> invoke)
    {
        return new RoutedEndpoint(
            new EndpointDescriptor("financial", name, path, desc,
                new[]
                {
                    new FieldDescriptor("code", "string", true, "SH600519"),
                    new FieldDescriptor("reportDates", "string[]", false, "",
                        Description: "可选，逗号分隔 yyyy-MM-dd；留空则由数据源自动拉取最近若干期"),
                    new FieldDescriptor("dateType", "enum", false, "ByReport",
                        new[] { "ByReport", "ByYear", "BySingleQuarter" }),
                    new FieldDescriptor("reportKind", "enum", false, "Cumulative",
                        new[] { "Cumulative", "SingleQuarter" }),
                    new FieldDescriptor("companyType", "enum", false, "Auto",
                        new[] { "Auto", "General", "Bank", "Insurance", "Securities" },
                        Description: "Auto = 由数据源嗅探"),
                },
                Protocol: "http"),
            async (body, c, ct) =>
            {
                var code = EndpointRunner.GetString(body, "code") ?? "SH600519";
                var dates = ParseReportDates(EndpointRunner.GetStringArray(body, "reportDates"));
                var dateType = EndpointRunner.GetEnum(body, "dateType", FinancialReportDateType.ByReport);
                var kind = EndpointRunner.GetEnum(body, "reportKind", FinancialReportKind.Cumulative);
                var ctStr = EndpointRunner.GetString(body, "companyType");
                CompanyType? companyType = null;
                if (!string.IsNullOrWhiteSpace(ctStr)
                    && !string.Equals(ctStr, "Auto", StringComparison.OrdinalIgnoreCase)
                    && Enum.TryParse<CompanyType>(ctStr, ignoreCase: true, out var ctVal))
                {
                    companyType = ctVal;
                }
                var req = new FinancialStatementRequest(code, dates, dateType, kind, companyType);
                Console.WriteLine($"[financial] {name} code={code} dateType={dateType} kind={kind} companyType={companyType?.ToString() ?? "auto"} dates={(dates is null ? "<auto>" : string.Join(",", dates))}");
                var rows = await invoke(c, req, ct).ConfigureAwait(false);
                return (rows.Count, (object?)rows);
            },
            // N-02：把 hedge 实际赢源透到 envelope 的 sources 字段。
            // FullBalanceSheetRow / FullIncomeStatementRow / FullCashFlowRow 都有 string Source 属性，
            // 反射式提取避免引入额外接口约束（也兼容未来加新源的 row 类型）。
            SourcesExtractor: EndpointRunner.ExtractSourcesFromRows);
    }

    private static IReadOnlyList<DateOnly>? ParseReportDates(string[] raw)
    {
        if (raw.Length == 0) return null;
        var list = new List<DateOnly>(raw.Length);
        foreach (var s in raw)
        {
            if (DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            {
                list.Add(d);
            }
        }
        return list.Count == 0 ? null : list;
    }

    // ── cninfo (1) ────────────────────────────────────
    // v1.3.0 Sprint 3：巨潮公告索引（单源 CninfoSource，不走对冲）。
    // PDF 下载走独立 GET /api/cninfo/pdf-download（流式），在 Program.cs 直接挂载。
    private static IEnumerable<RoutedEndpoint> BuildCninfo()
    {
        yield return new RoutedEndpoint(
            new EndpointDescriptor("cninfo", "QueryAnnouncementsAsync",
                "/api/cninfo/announcements",
                "查询巨潮公告列表（单源，不走对冲）。返回行含 adjunctUrl，可通过 GET /api/cninfo/pdf-download?adjunctUrl=... 下载 PDF。",
                new[]
                {
                    new FieldDescriptor("code", "string", true, "SH600519"),
                    new FieldDescriptor("category", "enum", false, "All",
                        new[] { "All", "AnnualReport", "SemiAnnualReport", "QuarterlyReport", "PerformanceForecast", "TemporaryAnnouncement" }),
                    new FieldDescriptor("startDate", "string", false, DaysAgo(90),
                        Description: "yyyy-MM-dd；留空则不限"),
                    new FieldDescriptor("endDate", "string", false, Today(),
                        Description: "yyyy-MM-dd；留空则不限"),
                    new FieldDescriptor("pageNum", "int", false, "1"),
                    new FieldDescriptor("pageSize", "int", false, "30"),
                },
                Protocol: "http"),
            async (body, c, ct) =>
            {
                var code = EndpointRunner.GetString(body, "code") ?? "SH600519";
                var category = EndpointRunner.GetEnum(body, "category", CninfoAnnouncementCategory.All);
                var start = ParseDateOnly(EndpointRunner.GetString(body, "startDate"));
                var end = ParseDateOnly(EndpointRunner.GetString(body, "endDate"));
                var pageNum = EndpointRunner.GetInt(body, "pageNum", 1);
                var pageSize = EndpointRunner.GetInt(body, "pageSize", 30);
                var req = new CninfoAnnouncementRequest(code, category, start, end, pageNum, pageSize);
                Console.WriteLine($"[cninfo] announcements code={code} category={category} start={start} end={end} page={pageNum}/{pageSize}");
                var rows = await c.QueryAnnouncementsAsync(req, ct).ConfigureAwait(false);
                return (rows.Count, (object?)rows);
            },
            // 单源端点也填一个常量 sources，让前端 envelope 字段在两类 HTTP 端点之间保持一致。
            SourcesExtractor: static _ => CninfoSources);
    }

    private static readonly IReadOnlyList<string> CninfoSources = new[] { "Cninfo" };

    private static DateOnly? ParseDateOnly(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }
}
