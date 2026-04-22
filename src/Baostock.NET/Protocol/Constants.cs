// 翻译自 reference/baostock-python/common/contants.py（baostock 00.9.10 客户端常量）
// 任何与上游 contants.py 偏离的命名/取值变更都必须在 docs/PROJECT-PLAN.md §3 中说明。

using System.ComponentModel;

namespace Baostock.NET.Protocol;

/// <summary>
/// 上游服务端连接参数。
/// </summary>
public static class BaostockServer
{
    /// <summary>BAOSTOCK_SERVER_IP：服务端主机名。</summary>
    public const string Host = "public-api.baostock.com";

    /// <summary>BAOSTOCK_SERVER_PORT：服务端端口。</summary>
    public const int Port = 10030;

    /// <summary>BAOSTOCK_CLIENT_VERSION：与上游 Python 客户端一致的协议版本号（出现在每条消息头）。</summary>
    public const string ClientVersion = "00.9.10";
}

/// <summary>
/// 帧/消息封装相关的固定常量。
/// </summary>
public static class Framing
{
    /// <summary>MESSAGE_SPLIT：消息内部各段分隔符（ASCII 0x01）。</summary>
    public const string MessageSplit = "\u0001";

    /// <summary>DELIMITER：一条消息物理结束符。</summary>
    public const string Delimiter = "\n";

    /// <summary>ATTRIBUTE_SPLIT：单字段内部属性间分隔符（如 fields 列表）。</summary>
    public const string AttributeSplit = ",";

    /// <summary>MESSAGE_HEADER_LENGTH：消息头固定长度（字节数）。</summary>
    public const int MessageHeaderLength = 21;

    /// <summary>MESSAGE_HEADER_BODYLENGTH：消息头中 body_length 字段所占的位数。</summary>
    public const int MessageBodyLengthDigits = 10;

    /// <summary>BAOSTOCK_PER_PAGE_COUNT：默认每页查询条数。</summary>
    public const int DefaultPerPageCount = 10000;

    /// <summary>STOCK_CODE_LENGTH：证券代码长度。</summary>
    public const int StockCodeLength = 9;
}

/// <summary>
/// 协议消息类型常量。值为 ASCII 双字符（"00"~"99"），与 contants.py 中 MESSAGE_TYPE_* 对应。
/// 历史/已下线/未实现的类型也保留，供 <see cref="FrameCodec"/> 解码兼容。
/// </summary>
public static class MessageTypes
{
    /// <summary>00 登录请求。</summary>
    public const string LoginRequest = "00";

    /// <summary>01 登录响应。</summary>
    public const string LoginResponse = "01";

    /// <summary>02 登出请求。</summary>
    public const string LogoutRequest = "02";

    /// <summary>03 登出响应。</summary>
    public const string LogoutResponse = "03";

    /// <summary>04 错误响应（服务端遇未知 MSG 类型也会返回此帧，body=error_code\u0001error_msg）。</summary>
    public const string Exception = "04";

    /// <summary>11 历史 K 线请求（旧版，已被 95 取代，保留兼容）。</summary>
    public const string GetKDataRequest = "11";

    /// <summary>12 历史 K 线响应（旧版，已被 96 取代，保留兼容）。</summary>
    public const string GetKDataResponse = "12";

    /// <summary>13 季频估值-股息分红 请求。</summary>
    public const string QueryDividendDataRequest = "13";

    /// <summary>14 季频估值-股息分红 响应。</summary>
    public const string QueryDividendDataResponse = "14";

    /// <summary>15 复权因子 请求。</summary>
    public const string AdjustFactorRequest = "15";

    /// <summary>16 复权因子 响应。</summary>
    public const string AdjustFactorResponse = "16";

    /// <summary>17 季频估值-盈利能力 请求。</summary>
    public const string ProfitDataRequest = "17";

    /// <summary>18 季频估值-盈利能力 响应。</summary>
    public const string ProfitDataResponse = "18";

    /// <summary>19 季频估值-营运能力 请求。</summary>
    public const string OperationDataRequest = "19";

    /// <summary>20 季频估值-营运能力 响应。</summary>
    public const string OperationDataResponse = "20";

    /// <summary>21 季频估值-成长能力 请求。</summary>
    public const string QueryGrowthDataRequest = "21";

    /// <summary>22 季频估值-成长能力 响应。</summary>
    public const string QueryGrowthDataResponse = "22";

    /// <summary>23 季频估值-杜邦指数 请求。</summary>
    public const string QueryDupontDataRequest = "23";

    /// <summary>24 季频估值-杜邦指数 响应。</summary>
    public const string QueryDupontDataResponse = "24";

    /// <summary>25 季频估值-偿债能力 请求。</summary>
    public const string QueryBalanceDataRequest = "25";

    /// <summary>26 季频估值-偿债能力 响应。</summary>
    public const string QueryBalanceDataResponse = "26";

    /// <summary>27 季频估值-现金流量 请求。</summary>
    public const string QueryCashFlowDataRequest = "27";

    /// <summary>28 季频估值-现金流量 响应。</summary>
    public const string QueryCashFlowDataResponse = "28";

    /// <summary>29 公司业绩快报 请求。</summary>
    public const string QueryPerformanceExpressReportRequest = "29";

    /// <summary>30 公司业绩快报 响应。</summary>
    public const string QueryPerformanceExpressReportResponse = "30";

    /// <summary>31 公司业绩预告 请求。</summary>
    public const string QueryForecastReportRequest = "31";

    /// <summary>32 公司业绩预告 响应。</summary>
    public const string QueryForecastReportResponse = "32";

    /// <summary>33 交易日 请求。</summary>
    public const string QueryTradeDatesRequest = "33";

    /// <summary>34 交易日 响应。</summary>
    public const string QueryTradeDatesResponse = "34";

    /// <summary>35 全部证券列表 请求。</summary>
    public const string QueryAllStockRequest = "35";

    /// <summary>36 全部证券列表 响应。</summary>
    public const string QueryAllStockResponse = "36";

    /// <summary>45 证券基本资料 请求。</summary>
    public const string QueryStockBasicRequest = "45";

    /// <summary>46 证券基本资料 响应。</summary>
    public const string QueryStockBasicResponse = "46";

    /// <summary>47 存款利率 请求。</summary>
    public const string QueryDepositRateDataRequest = "47";

    /// <summary>48 存款利率 响应。</summary>
    public const string QueryDepositRateDataResponse = "48";

    /// <summary>49 贷款利率 请求。</summary>
    public const string QueryLoanRateDataRequest = "49";

    /// <summary>50 贷款利率 响应。</summary>
    public const string QueryLoanRateDataResponse = "50";

    /// <summary>51 存款准备金率 请求。</summary>
    public const string QueryRequiredReserveRatioDataRequest = "51";

    /// <summary>52 存款准备金率 响应。</summary>
    public const string QueryRequiredReserveRatioDataResponse = "52";

    /// <summary>53 货币供应量(月度) 请求。</summary>
    public const string QueryMoneySupplyDataMonthRequest = "53";

    /// <summary>54 货币供应量(月度) 响应。</summary>
    public const string QueryMoneySupplyDataMonthResponse = "54";

    /// <summary>55 货币供应量(年底余额) 请求。</summary>
    public const string QueryMoneySupplyDataYearRequest = "55";

    /// <summary>56 货币供应量(年底余额) 响应。</summary>
    public const string QueryMoneySupplyDataYearResponse = "56";

    /// <summary>57 银行间同业拆放利率 请求（仅协议常量，无 Python 实现，v1.0 不实现）。</summary>
    public const string QueryShiborDataRequest = "57";

    /// <summary>58 银行间同业拆放利率 响应（仅协议常量，无 Python 实现，v1.0 不实现）。</summary>
    public const string QueryShiborDataResponse = "58";

    /// <summary>59 行业类别 请求。</summary>
    public const string QueryStockIndustryRequest = "59";

    /// <summary>60 行业类别 响应。</summary>
    public const string QueryStockIndustryResponse = "60";

    /// <summary>61 沪深 300 成分股 请求。</summary>
    public const string QueryHs300StocksRequest = "61";

    /// <summary>62 沪深 300 成分股 响应。</summary>
    public const string QueryHs300StocksResponse = "62";

    /// <summary>63 上证 50 成分股 请求。</summary>
    public const string QuerySz50StocksRequest = "63";

    /// <summary>64 上证 50 成分股 响应。</summary>
    public const string QuerySz50StocksResponse = "64";

    /// <summary>65 中证 500 成分股 请求。</summary>
    public const string QueryZz500StocksRequest = "65";

    /// <summary>66 中证 500 成分股 响应。</summary>
    public const string QueryZz500StocksResponse = "66";

    /// <summary>67 终止上市股票 请求（v1.0 必交付）。</summary>
    public const string QueryTerminatedStocksRequest = "67";

    /// <summary>68 终止上市股票 响应（v1.0 必交付）。</summary>
    public const string QueryTerminatedStocksResponse = "68";

    /// <summary>69 暂停上市股票 请求（v1.0 必交付）。</summary>
    public const string QuerySuspendedStocksRequest = "69";

    /// <summary>70 暂停上市股票 响应（v1.0 必交付）。</summary>
    public const string QuerySuspendedStocksResponse = "70";

    /// <summary>71 ST 股票 请求（v1.0 必交付）。</summary>
    public const string QueryStStocksRequest = "71";

    /// <summary>72 ST 股票 响应（v1.0 必交付）。</summary>
    public const string QueryStStocksResponse = "72";

    /// <summary>73 *ST 股票 请求（v1.0 必交付）。</summary>
    public const string QueryStarStStocksRequest = "73";

    /// <summary>74 *ST 股票 响应（v1.0 必交付）。</summary>
    public const string QueryStarStStocksResponse = "74";

    /// <summary>75 居民价格消费指数 请求（服务端已下线 ec=10004020，v1.0 不实现）。</summary>
    public const string QueryCpiDataRequest = "75";

    /// <summary>76 居民价格消费指数 响应（服务端已下线，v1.0 不实现）。</summary>
    public const string QueryCpiDataResponse = "76";

    /// <summary>77 工业品出厂价格指数 请求（服务端已下线，v1.0 不实现）。</summary>
    public const string QueryPpiDataRequest = "77";

    /// <summary>78 工业品出厂价格指数 响应（服务端已下线，v1.0 不实现）。</summary>
    public const string QueryPpiDataResponse = "78";

    /// <summary>79 采购经理人指数 请求（服务端已下线，v1.0 不实现）。</summary>
    public const string QueryPmiDataRequest = "79";

    /// <summary>80 采购经理人指数 响应（服务端已下线，v1.0 不实现）。</summary>
    public const string QueryPmiDataResponse = "80";

    /// <summary>81 概念分类 请求（服务端已下线，v1.0 不实现）。</summary>
    public const string QueryStockConceptRequest = "81";

    /// <summary>82 概念分类 响应（服务端已下线，v1.0 不实现）。</summary>
    public const string QueryStockConceptResponse = "82";

    /// <summary>83 地域分类 请求（服务端已下线，v1.0 不实现）。</summary>
    public const string QueryStockAreaRequest = "83";

    /// <summary>84 地域分类 响应（服务端已下线，v1.0 不实现）。</summary>
    public const string QueryStockAreaResponse = "84";

    /// <summary>85 中小板分类 请求（服务端已下线，v1.0 不实现）。</summary>
    public const string QueryAmeStocksRequest = "85";

    /// <summary>86 中小板分类 响应（服务端已下线，v1.0 不实现）。</summary>
    public const string QueryAmeStocksResponse = "86";

    /// <summary>87 创业板分类 请求（服务端已下线，v1.0 不实现）。</summary>
    public const string QueryGemStocksRequest = "87";

    /// <summary>88 创业板分类 响应（服务端已下线，v1.0 不实现）。</summary>
    public const string QueryGemStocksResponse = "88";

    /// <summary>89 沪港通 请求（服务端已下线，v1.0 不实现）。</summary>
    public const string QueryShhkStocksRequest = "89";

    /// <summary>90 沪港通 响应（服务端已下线，v1.0 不实现）。</summary>
    public const string QueryShhkStocksResponse = "90";

    /// <summary>91 深港通 请求（服务端已下线，v1.0 不实现）。</summary>
    public const string QuerySzhkStocksRequest = "91";

    /// <summary>92 深港通 响应（服务端已下线，v1.0 不实现）。</summary>
    public const string QuerySzhkStocksResponse = "92";

    /// <summary>93 风险警示板分类 请求（服务端已下线，v1.0 不实现）。</summary>
    public const string QueryStocksInRiskRequest = "93";

    /// <summary>94 风险警示板分类 响应（服务端已下线，v1.0 不实现）。</summary>
    public const string QueryStocksInRiskResponse = "94";

    /// <summary>95 历史 K 线 Plus 请求。</summary>
    public const string GetKDataPlusRequest = "95";

    /// <summary>96 历史 K 线 Plus 响应（响应 body 走 zlib 压缩）。</summary>
    public const string GetKDataPlusResponse = "96";

    /// <summary>
    /// COMPRESSED_MESSAGE_TYPE_TUPLE：服务端响应 body 需 zlib 解压的消息类型集合。
    /// </summary>
    public static readonly IReadOnlySet<string> CompressedResponses = new HashSet<string>
    {
        GetKDataPlusResponse,
    };
}

/// <summary>
/// 服务端错误码（与 contants.py 中 BSERR_* 一一对应）。
/// </summary>
public static class BaostockErrorCode
{
    /// <summary>正确返回值。</summary>
    [Description("正确返回值")] public const string Success = "0";

    /// <summary>用户未登陆。</summary>
    [Description("用户未登陆")] public const string NoLogin = "10001001";

    /// <summary>用户名或密码错误。</summary>
    [Description("用户名或密码错误")] public const string UsernameOrPasswordErr = "10001002";

    /// <summary>获取用户信息失败。</summary>
    [Description("获取用户信息失败")] public const string GetUserInfoFail = "10001003";

    /// <summary>客户端版本号过期。</summary>
    [Description("客户端版本号过期")] public const string ClientVersionExpire = "10001004";

    /// <summary>账号登陆数达到上限。</summary>
    [Description("账号登陆数达到上限")] public const string LoginCountLimit = "10001005";

    /// <summary>用户权限不足。</summary>
    [Description("用户权限不足")] public const string AccessInsufficience = "10001006";

    /// <summary>需要登录激活。</summary>
    [Description("需要登录激活")] public const string NeedActivate = "10001007";

    /// <summary>用户名为空。</summary>
    [Description("用户名为空")] public const string UsernameEmpty = "10001008";

    /// <summary>密码为空。</summary>
    [Description("密码为空")] public const string PasswordEmpty = "10001009";

    /// <summary>用户登出失败。</summary>
    [Description("用户登出失败")] public const string LogoutFail = "10001010";

    /// <summary>黑名单用户。</summary>
    [Description("黑名单用户")] public const string BlacklistUser = "10001011";

    /// <summary>网络错误。</summary>
    [Description("网络错误")] public const string SocketErr = "10002001";

    /// <summary>网络连接失败。</summary>
    [Description("网络连接失败")] public const string ConnectFail = "10002002";

    /// <summary>网络连接超时。</summary>
    [Description("网络连接超时")] public const string ConnectTimeout = "10002003";

    /// <summary>网络接收时连接断开。</summary>
    [Description("网络接收时连接断开")] public const string RecvConnectionClosed = "10002004";

    /// <summary>网络发送失败。</summary>
    [Description("网络发送失败")] public const string SendSockFail = "10002005";

    /// <summary>网络发送超时。</summary>
    [Description("网络发送超时")] public const string SendSockTimeout = "10002006";

    /// <summary>网络接收错误。</summary>
    [Description("网络接收错误")] public const string RecvSockFail = "10002007";

    /// <summary>网络接收超时。</summary>
    [Description("网络接收超时")] public const string RecvSockTimeout = "10002008";

    /// <summary>解析数据错误。</summary>
    [Description("解析数据错误")] public const string ParseDataErr = "10004001";

    /// <summary>gzip 解压失败。</summary>
    [Description("gzip 解压失败")] public const string UngzipDataFail = "10004002";

    /// <summary>客户端未知错误。</summary>
    [Description("客户端未知错误")] public const string UnknownErr = "10004003";

    /// <summary>数组越界。</summary>
    [Description("数组越界")] public const string OutOfBounds = "10004004";

    /// <summary>传入参数为空。</summary>
    [Description("传入参数为空")] public const string InParamEmpty = "10004005";

    /// <summary>参数错误。</summary>
    [Description("参数错误")] public const string ParamErr = "10004006";

    /// <summary>起始日期格式不正确。</summary>
    [Description("起始日期格式不正确")] public const string StartDateErr = "10004007";

    /// <summary>截止日期格式不正确。</summary>
    [Description("截止日期格式不正确")] public const string EndDateErr = "10004008";

    /// <summary>起始日期大于终止日期。</summary>
    [Description("起始日期大于终止日期")] public const string StartBigThanEnd = "10004009";

    /// <summary>日期格式不正确。</summary>
    [Description("日期格式不正确")] public const string DateErr = "10004010";

    /// <summary>无效的证券代码。</summary>
    [Description("无效的证券代码")] public const string CodeInvalied = "10004011";

    /// <summary>无效的指标。</summary>
    [Description("无效的指标")] public const string IndicatorInvalied = "10004012";

    /// <summary>超出日期支持范围。</summary>
    [Description("超出日期支持范围")] public const string BeyondDateSupport = "10004013";

    /// <summary>不支持的混合证券品种。</summary>
    [Description("不支持的混合证券品种")] public const string MixedCodesMarket = "10004014";

    /// <summary>不支持的证券代码品种。</summary>
    [Description("不支持的证券代码品种")] public const string NoSupportCodesMarket = "10004015";

    /// <summary>交易条数超过上限。</summary>
    [Description("交易条数超过上限")] public const string OrderToUpperLimit = "10004016";

    /// <summary>不支持的交易信息。</summary>
    [Description("不支持的交易信息")] public const string NoSupportOrderInfo = "10004017";

    /// <summary>指标重复。</summary>
    [Description("指标重复")] public const string IndicatorRepeat = "10004018";

    /// <summary>消息格式不正确。</summary>
    [Description("消息格式不正确")] public const string MessageError = "10004019";

    /// <summary>错误的消息类型（服务端不支持/已下线的 MSG 类型）。</summary>
    [Description("错误的消息类型")] public const string MessageCodeError = "10004020";

    /// <summary>系统级别错误。</summary>
    [Description("系统级别错误")] public const string SystemError = "10005001";
}
