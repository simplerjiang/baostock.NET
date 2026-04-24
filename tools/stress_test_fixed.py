import asyncio
import aiohttp
import time

FIXED_TESTS = {}

# 巨潮：加 Accept-Encoding 和 User-Agent (Fix 1 验证通过)
FIXED_TESTS["巨潮公告(已修复)"] = {
    "url": "http://www.cninfo.com.cn/new/hisAnnouncement/query",
    "method": "POST",
    "headers": {
        "Content-Type": "application/x-www-form-urlencoded",
        "Accept-Encoding": "gzip, deflate",
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
    },
    "data": "stock=000001,gssz0000001&tabName=fulltext&pageSize=5&pageNum=1&column=szse&category=category_ndbg_szsh",
}

# 研报：方式2/3 验证通过 — 使用带 qType 参数 + User-Agent
FIXED_TESTS["东方财富研报(已修复)"] = {
    "url": "https://reportapi.eastmoney.com/report/list?industryCode=*&pageNo=1&pageSize=5&fields=&qType=0&orgCode=&rptType=&author=&beginTime=&endTime=&code=000001",
    "method": "GET",
    "headers": {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"},
}

CONCURRENCY_LEVELS = [1, 5, 10, 20, 50]

async def single_request(session, test_config, semaphore):
    async with semaphore:
        start = time.monotonic()
        try:
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
    print("修复后接口并发压力测试")
    print("=" * 80)
    
    for name, config in FIXED_TESTS.items():
        print(f"\n{'─' * 60}")
        print(f"接口: {name}")
        print(f"{'─' * 60}")
        
        for concurrency in CONCURRENCY_LEVELS:
            total = concurrency * 3
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
            
            if result["success_rate"] < 80:
                print(f"    ⛔ 成功率过低，跳过更高并发")
                break
            
            await asyncio.sleep(1)
        
        await asyncio.sleep(2)

if __name__ == "__main__":
    asyncio.run(main())
