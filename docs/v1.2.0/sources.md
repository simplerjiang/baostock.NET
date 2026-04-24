> v1.2.0 专集 | 面向从 v1.0.x 升级的用户 | 2026-04-24

# v1.2.0 第三方数据源

本文档记录 v1.2.0 接入的所有 HTTP 数据源、它们的字段协议、限制以及已知陷阱。

## 1. 设计原则

- **不爬网页**:只使用公开 JSON / JSONP / 文本 API,不做 HTML 解析
- **不绕反爬**:不伪造复杂 cookie / token 流程,仅设置必要的 UA / Referer
- **限速可观测**:所有源的失败状态都记录在 `SourceHealthRegistry`,故障会自动降级到备用源
- **首成功胜出**:hedge 模型保证就算 P=0 卡住,500 ms 内备用源会顶上

## 2. 实时行情三源

### 对比表

| 源 | 优先级 | 端点 | 编码 | Referer | 批量支持 | Bid/Ask |
|---|---|---|---|---|---|---|
| **Sina** | P=0 | `hq.sinajs.cn/list={codes}` | GBK | **必需**(`https://finance.sina.com.cn`) | 逗号分隔 | ✓ 5 档 |
| **Tencent** | P=1 | `qt.gtimg.cn/q={codes}` | GBK | — | 逗号分隔 | ✓ 5 档 |
| **EastMoney** | P=2 | `push2.eastmoney.com/api/qt/stock/get` | JSON (UTF-8) | **必需** + UT token | 单 secid(并发) | ✗(返回 null) |

### 字段解析规则

#### Sina(JSONP 文本,`,` 分隔 32+ 字段)

返回样例:`var hq_str_sh600519="贵州茅台,1700.00,1680.00,...";`。截取 `=` 后到 `;` 前,去引号,按 `,` 切分:

| 索引 | 字段 |
|---|---|
| 0 | Name(股票名称) |
| 1 | Open(开盘价) |
| 2 | PreClose(昨收) |
| 3 | Last(最新价) |
| 4 | High(最高) |
| 5 | Low(最低) |
| 8 | Volume(成交量,**股**) |
| 9 | Amount(成交额,**元**) |
| 11-20 | Bid 1-5 价量 |
| 21-30 | Ask 1-5 价量 |

#### Tencent(JSONP 文本,`~` 分隔 35+ 字段)

返回样例:`v_sh600519="1~贵州茅台~600519~1700.00~...";`。按 `~` 切分:

| 索引 | 字段 |
|---|---|
| 1 | Name |
| 3 | Last |
| 4 | PreClose |
| 5 | Open |
| 6 | Volume(**手** → 需 ×100 得股) |
| 9-18 | Bid 1-5 |
| 19-28 | Ask 1-5 |
| 33 | High |
| 34 | Low |
| 37 | Amount(**万元** → 需 ×10000 得元) |

#### EastMoney(JSON)

返回 `{ "data": { "f43": ..., ... } }`,关键 key:

| Key | 字段 | 备注 |
|---|---|---|
| f43 | Last | 需 / 10^f152 |
| f44 | High | 需 / 10^f152 |
| f45 | Low | 需 / 10^f152 |
| f46 | Open | 需 / 10^f152 |
| f47 | Volume(手) | — |
| f48 | Amount(元) | — |
| f60 | PreClose | 需 / 10^f152 |
| f86 | 行情时间 | Unix timestamp |
| **f152** | **小数位数** | **反归一化因子,所有价格字段必读** |

**陷阱**:EM 返回的价格是整数 * 10^f152,读取时必须除以 `Math.Pow(10, f152)`,否则会得到放大 100 倍的价格。

## 3. 历史 K 线双源

### 对比表

| 源 | 优先级 | 端点数 | 字段 | 备注 |
|---|---|---|---|---|
| **EastMoney** | P=0 | 单端点 | 11 字段全 | 推荐 |
| **Tencent** | P=1 | 3 端点分发 | 6 字段(缺 Amount / Amplitude / 涨跌幅 / 换手率) | 备用 |

### EastMoney 端点

```
https://push2his.eastmoney.com/api/qt/stock/kline/get
    ?secid={secid}
    &klt={frequency}
    &fqt={adjust}
    &beg={start}
    &end={end}
    &lmt=1000000
    &fields1=...&fields2=...
```

- 返回 `data.klines`:字符串数组,每行按 `,` 分隔 11 字段
- 字段顺序:`date, open, close, high, low, volume, amount, amplitude, pct, chg, turnover`

### Tencent 三端点(按频率和复权分发)

| 端点 | 用途 |
|---|---|
| `web.ifzq.gtimg.cn/appstock/app/kline/kline` | 日/周/月 **不复权** |
| `web.ifzq.gtimg.cn/appstock/app/fqkline/get` | 日/周/月 **前复权 / 后复权** |
| `web.ifzq.gtimg.cn/appstock/app/kline/mkline` | 分钟线 |

**腾讯字段顺序陷阱**:返回 `[date, open, close, high, low, volume]` —— **close 在 high 之前**!如果你直接照搬 EM 的字段顺序会得到完全错乱的 K 线。

## 4. Frequency 与 Adjust 映射表

### Frequency(K 线周期)

| 内部枚举 | EastMoney klt | Tencent param |
|---|---|---|
| Minute5 | 5 | m5 |
| Minute15 | 15 | m15 |
| Minute30 | 30 | m30 |
| Minute60 | 60 | m60 |
| Daily | 101 | day |
| Weekly | 102 | week |
| Monthly | 103 | month |

### Adjust(复权方式)

| 内部枚举 | EastMoney fqt | Tencent 端点 |
|---|---|---|
| None(不复权) | 0 | kline/kline |
| Forward(前复权) | 1 | fqkline/get + qfq |
| Backward(后复权) | 2 | fqkline/get + hfq |

## 5. SecId 映射(东财 secid 格式)

东财所有 API 要求 `secid = {market}.{code}`:

| 市场 | secid 前缀 | 示例 |
|---|---|---|
| SH(上交所) | `1.` | `1.600519` |
| SZ(深交所) | `0.` | `0.000001` |
| **BJ(北交所)** | `116.` | `116.832000`(**未经生产验证,作备用源用途**) |

## 6. CodeFormatter 支持的输入格式

输入端向后兼容以下 6 种写法,内部统一转为东财风格 `SH600519`:

1. `SH600519` / `SZ000001` — **标准格式**(东财风格,大写,无点)
2. `sh.600519` / `sz.000001` — baostock native(v1.0.x 默认)
3. `sh600519` / `sz000001` — 小写无点
4. `600519.SH` / `000001.SZ` — Wind 风格
5. `1.600519` / `0.000001` — 东财 secid
6. `600519` / `000001` — 纯数字(按首位自动判断市场:`6` → SH,`0/3` → SZ,`4/8/9` → BJ)

## 7. 已知限制

### BJ 历史 K 线公网瘫痪

- EastMoney `116.` 端点返回 `ResponseEnded`(连接被上游掐断)
- Tencent 三端点对 BJ 股票返回空数组 `"data":[]`
- 结果:`AllSourcesFailedException(DataKind.KLine)`
- 规避:对 BJ 历史 K 线仍使用 baostock TCP 通道(`client.QueryHistoryKDataPlusAsync`)

### BJ 实时行情盘前陈旧

- 09:00 之前:Sina 返回全零、Tencent 缓存昨收
- 开盘后恢复正常
- 规避:业务层判断交易时间,盘前不调用实时行情

### 指数代码限制

- `SH000001`(上证指数)等指数代码仅适用于 baostock TCP 查询
- 不要用在 `GetRealtimeQuoteAsync` / `GetHistoryKLineAsync` 多源 HTTP 通道上,三方源对指数的字段协议与个股不同,当前实现只覆盖个股
