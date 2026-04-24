import asyncio
import aiohttp
import time
import sys

TESTS = {
    "腾讯实时行情": {
        "url": "https://qt.gtimg.cn/q=sz000001",
        "method": "GET",
        "headers": {},
    },
    "新浪实时行情": {
        "url": "https://hq.sinajs.cn/list=sz000001",
        "method": "GET",
        "headers": {"Referer": "https://finance.sina.com.cn"},
    },
    "东方财富历史K线": {
        "url": "https://push2his.eastmoney.com/api/qt/stock/kline/get?secid=0.000001&fields1=f1,f2,f3,f4,f5,f6&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61&klt=101&fqt=1&end=20500101&lmt=20",
        "method": "GET",
        "headers": {},
    },
    "东方财富实时行情": {
        "url": "https://push2.eastmoney.com/api/qt/stock/get?secid=0.000001&fields=f43,f44,f45,f46,f47,f48,f57,f58,f170",
        "method": "GET",
        "headers": {},
    },
    "东方财富研报": {
        "url": "https://reportapi.eastmoney.com/report/list?industryCode=*&pageNo=1&pageSize=5&code=000001",
        "method": "GET",
        "headers": {},
    },
    "东方财富股东": {
        "url": "https://emweb.securities.eastmoney.com/PC_HSF10/ShareholderResearch/PageAjax?code=SZ000001",
        "method": "GET",
        "headers": {},
    },
    "东方财富GDP": {
        "url": "https://datacenter-web.eastmoney.com/api/data/v1/get?columns=ALL&pageNumber=1&pageSize=10&sortColumns=REPORT_DATE&sortTypes=-1&reportName=RPT_ECONOMY_GDP",
        "method": "GET",
        "headers": {},
    },
    "新浪财报": {
        "url": "https://quotes.sina.cn/cn/api/openapi.php/CompanyFinanceService.getFinanceReport2022?paperCode=sh600519&source=fzb&type=0&page=1&num=5",
        "method": "GET",
        "headers": {},
    },
    "巨潮公告": {
        "url": "http://www.cninfo.com.cn/new/hisAnnouncement/query",
        "method": "POST",
        "headers": {"Content-Type": "application/x-www-form-urlencoded"},
        "data": "stock=000001,gssz0000001&tabName=fulltext&pageSize=5&pageNum=1&column=szse&category=category_ndbg_szsh",
    },
}

CONCURRENCY_LEVELS = [1, 5, 10, 20, 50]

async def single_request(session, test_config, semaphore):
    async with semaphore:
        try:
            start = time.monotonic()
            if test_config["method"] == "POST":
                async with session.post(
                    test_config["url"],
                    headers=test_config["headers"],
                    data=test_config.get("data", ""),
                    timeout=aiohttp.ClientTimeout(total=15)
                ) as resp:
                    status = resp.status
                    body_len = len(await resp.read())
            else:
                async with session.get(
                    test_config["url"],
                    headers=test_config["headers"],
                    timeout=aiohttp.ClientTimeout(total=15)
                ) as resp:
                    status = resp.status
                    body_len = len(await resp.read())
            elapsed = time.monotonic() - start
            return status, elapsed, body_len, None
        except Exception as e:
            elapsed = time.monotonic() - start
            return 0, elapsed, 0, str(e)

async def stress_test_api(name, test_config, concurrency, total_requests):
    semaphore = asyncio.Semaphore(concurrency)
    connector = aiohttp.TCPConnector(limit=concurrency, force_close=True)
    async with aiohttp.ClientSession(connector=connector) as session:
        tasks = [single_request(session, test_config, semaphore) for _ in range(total_requests)]
        results = await asyncio.gather(*tasks)
    
    statuses = {}
    errors = []
    latencies = []
    for status, elapsed, body_len, error in results:
        if error:
            errors.append(error)
        else:
            statuses[status] = statuses.get(status, 0) + 1
            latencies.append(elapsed)
    
    avg_latency = sum(latencies) / len(latencies) if latencies else 0
    p95_latency = sorted(latencies)[int(len(latencies) * 0.95)] if latencies else 0
    
    return {
        "statuses": statuses,
        "errors": len(errors),
        "error_samples": errors[:3],
        "avg_latency_ms": round(avg_latency * 1000),
        "p95_latency_ms": round(p95_latency * 1000),
        "success_rate": round(statuses.get(200, 0) / total_requests * 100, 1),
    }

async def main():
    print("=" * 80)
    print("A股数据接口并发压力测试")
    print("=" * 80)
    
    for name, config in TESTS.items():
        print(f"\n{'─' * 60}")
        print(f"接口: {name}")
        print(f"{'─' * 60}")
        
        for concurrency in CONCURRENCY_LEVELS:
            total = concurrency * 3  # 每个并发级别发 3x 请求
            result = await stress_test_api(name, config, concurrency, total)
            
            status_str = " ".join(f"{k}:{v}" for k, v in sorted(result["statuses"].items()))
            print(f"  并发={concurrency:>3}, 请求={total:>4} | "
                  f"成功率={result['success_rate']:>5.1f}% | "
                  f"延迟 avg={result['avg_latency_ms']:>5}ms p95={result['p95_latency_ms']:>5}ms | "
                  f"状态: {status_str} | "
                  f"错误: {result['errors']}")
            
            if result["error_samples"]:
                for e in result["error_samples"][:1]:
                    print(f"    ⚠ {e[:100]}")
            
            # 如果成功率低于 80%，跳过更高并发
            if result["success_rate"] < 80:
                print(f"    ⛔ 成功率过低，跳过更高并发")
                break
            
            # 每轮之间等 1s 避免影响下一轮
            await asyncio.sleep(1)
        
        # 每个接口之间等 2s
        await asyncio.sleep(2)

if __name__ == "__main__":
    asyncio.run(main())
