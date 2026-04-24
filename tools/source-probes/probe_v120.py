# -*- coding: utf-8 -*-
"""
Baostock.NET v1.2.0 Sprint 0 — Cross-source Probe Script
========================================================

Hits Tencent / Sina / EastMoney / CNINFO endpoints for sample stocks
and dumps raw responses to tools/source-probes/raw/.

Sample stocks: SH600519 (Kweichow Moutai), SZ000001 (Ping An Bank)
Reporting period: 2024-12-31

Run:
    python tools\\source-probes\\probe_v120.py
"""

from __future__ import annotations

import json
import os
import sys
import time
import traceback
from pathlib import Path
from typing import Any

import requests

ROOT = Path(__file__).resolve().parent
RAW = ROOT / "raw"
RAW.mkdir(parents=True, exist_ok=True)

TIMEOUT = 15
SUMMARY: list[dict[str, Any]] = []

UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
    "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36"
)


def write_text(name: str, text: str) -> Path:
    p = RAW / name
    p.write_text(text, encoding="utf-8")
    return p


def write_bytes(name: str, data: bytes) -> Path:
    p = RAW / name
    p.write_bytes(data)
    return p


def record(source: str, api: str, symbol: str, ok: bool, status: int | None,
           file: str, note: str = "") -> None:
    SUMMARY.append({
        "source": source, "api": api, "symbol": symbol,
        "ok": ok, "status": status, "file": file, "note": note,
    })


def do_get(source: str, api: str, symbol: str, url: str,
           headers: dict[str, str] | None = None,
           filename: str | None = None,
           is_binary: bool = False,
           encoding: str | None = None) -> requests.Response | None:
    fn = filename or f"{source}_{api}_{symbol}.txt"
    try:
        r = requests.get(url, headers=headers or {"User-Agent": UA},
                         timeout=TIMEOUT, stream=is_binary)
        if is_binary:
            # store first 4KB only
            body = r.raw.read(4096, decode_content=True)
            write_bytes(fn, body)
            note = f"binary, captured {len(body)} bytes; content-type={r.headers.get('Content-Type','')}"
        else:
            if encoding:
                r.encoding = encoding
            else:
                # Try to honor server-declared encoding; fall back to apparent
                if not r.encoding or r.encoding.lower() == "iso-8859-1":
                    r.encoding = r.apparent_encoding or "utf-8"
            write_text(fn, r.text)
            note = f"len={len(r.text)} chars; content-type={r.headers.get('Content-Type','')}"
        record(source, api, symbol, r.ok, r.status_code, fn, note)
        return r
    except Exception as ex:
        msg = f"GET {url}\nERROR: {type(ex).__name__}: {ex}\n\n{traceback.format_exc()}"
        write_text(fn, msg)
        record(source, api, symbol, False, None, fn, f"exception: {type(ex).__name__}: {ex}")
        return None


def do_post(source: str, api: str, symbol: str, url: str,
            data: Any = None, headers: dict[str, str] | None = None,
            filename: str | None = None) -> requests.Response | None:
    fn = filename or f"{source}_{api}_{symbol}.txt"
    try:
        r = requests.post(url, data=data, headers=headers or {"User-Agent": UA},
                          timeout=TIMEOUT)
        if not r.encoding or r.encoding.lower() == "iso-8859-1":
            r.encoding = r.apparent_encoding or "utf-8"
        write_text(fn, r.text)
        record(source, api, symbol, r.ok, r.status_code, fn,
               f"POST len={len(r.text)} chars; content-type={r.headers.get('Content-Type','')}")
        return r
    except Exception as ex:
        msg = f"POST {url}\nDATA: {data!r}\nERROR: {type(ex).__name__}: {ex}\n\n{traceback.format_exc()}"
        write_text(fn, msg)
        record(source, api, symbol, False, None, fn, f"exception: {type(ex).__name__}: {ex}")
        return None


# ---------------------------------------------------------------------------
# 1. Realtime quotes
# ---------------------------------------------------------------------------

def realtime():
    for sym, low in (("SH600519", "sh600519"), ("SZ000001", "sz000001")):
        # Tencent
        do_get("tencent", "realtime", sym,
               f"https://qt.gtimg.cn/q={low}",
               encoding="gbk")
        # Sina
        do_get("sina", "realtime", sym,
               f"https://hq.sinajs.cn/list={low}",
               headers={"User-Agent": UA, "Referer": "https://finance.sina.com.cn"},
               encoding="gbk")
        # EastMoney
        secid = ("1." if sym.startswith("SH") else "0.") + sym[2:]
        em_fields = ("f43,f44,f45,f46,f60,f47,f48,f49,f50,f51,f52,f57,f58,"
                     "f168,f169,f170,f171,f47,f48,f86,f117,f162,f152,f167,"
                     "f164,f163,f116,f60,f45,f44,f43,f46,f51,f52,f191,f192")
        do_get("eastmoney", "realtime", sym,
               f"https://push2.eastmoney.com/api/qt/stock/get?secid={secid}&fields={em_fields}")


# ---------------------------------------------------------------------------
# 2. Historical K-Line (daily, last 30 bars)
# ---------------------------------------------------------------------------

def kline():
    for sym in ("SH600519", "SZ000001"):
        secid = ("1." if sym.startswith("SH") else "0.") + sym[2:]
        do_get("eastmoney", "kline_day", sym,
               f"https://push2his.eastmoney.com/api/qt/stock/kline/get"
               f"?secid={secid}&klt=101&fqt=1&lmt=30"
               f"&fields1=f1,f2,f3,f4,f5,f6"
               f"&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61")
        low = sym.lower()
        # Tencent qfq day
        do_get("tencent", "kline_day_qfq", sym,
               f"https://web.ifzq.gtimg.cn/appstock/app/fqkline/get?param={low},day,,,30,qfq")
        # Tencent unadjusted day for reference
        do_get("tencent", "kline_day_raw", sym,
               f"https://web.ifzq.gtimg.cn/appstock/app/kline/kline?param={low},day,,,30,")
        # 5-min
        do_get("eastmoney", "kline_5m", sym,
               f"https://push2his.eastmoney.com/api/qt/stock/kline/get"
               f"?secid={secid}&klt=5&fqt=1&lmt=30"
               f"&fields1=f1,f2,f3,f4,f5,f6"
               f"&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61")
        do_get("tencent", "kline_5m", sym,
               f"https://web.ifzq.gtimg.cn/appstock/app/kline/mkline?param={low},m5,,30")


# ---------------------------------------------------------------------------
# 3. Financial reports (annual 2024-12-31)
# ---------------------------------------------------------------------------

def financial():
    for sym in ("SH600519", "SZ000001"):
        # EastMoney — date list (companyType=4 generic listed company)
        do_get("eastmoney", "zcfzb_dates", sym,
               f"https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/zcfzbDateAjaxNew"
               f"?companyType=4&reportDateType=0&code={sym}")
        # Balance sheet for 2024-12-31
        do_get("eastmoney", "zcfzb", sym,
               f"https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/zcfzbAjaxNew"
               f"?companyType=4&reportDateType=0&reportType=1&dates=2024-12-31&code={sym}")
        # Income statement
        do_get("eastmoney", "lrb_dates", sym,
               f"https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/lrbDateAjaxNew"
               f"?companyType=4&reportDateType=0&code={sym}")
        do_get("eastmoney", "lrb", sym,
               f"https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/lrbAjaxNew"
               f"?companyType=4&reportDateType=0&reportType=1&dates=2024-12-31&code={sym}")
        # Cash flow
        do_get("eastmoney", "xjllb_dates", sym,
               f"https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/xjllbDateAjaxNew"
               f"?companyType=4&reportDateType=0&code={sym}")
        do_get("eastmoney", "xjllb", sym,
               f"https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/xjllbAjaxNew"
               f"?companyType=4&reportDateType=0&reportType=1&dates=2024-12-31&code={sym}")
        # Sina four reports
        low = sym.lower()
        for kind in ("fzb", "lrb", "llb", "gjzb"):
            do_get("sina", f"finance_{kind}", sym,
                   f"https://quotes.sina.cn/cn/api/openapi.php/CompanyFinanceService.getFinanceReport2022"
                   f"?paperCode={low}&source={kind}&type=0&page=1&num=10",
                   headers={"User-Agent": UA, "Referer": "https://finance.sina.com.cn"})


# ---------------------------------------------------------------------------
# 4. Deep data: research / shareholders / billboard / GDP
# ---------------------------------------------------------------------------

def deep():
    sym = "SH600519"
    code6 = "600519"
    # Research reports
    do_get("eastmoney", "research", sym,
           "https://reportapi.eastmoney.com/report/list"
           "?industryCode=*&pageSize=10&industry=*&rating=&ratingChange="
           "&beginTime=2025-01-01&endTime=2026-04-24&pageNo=1&fields="
           f"&qType=0&orgCode=&code={code6}&rptType=&author=")
    # Shareholders
    do_get("eastmoney", "shareholders", sym,
           f"https://emweb.securities.eastmoney.com/PC_HSF10/ShareholderResearch/PageAjax?code={sym}")
    # Billboard (lhb) - 600519 rarely makes the list; we just need shape, and
    # also try a pageSize=20 dump for any code to verify shape if 600519 empty.
    do_get("eastmoney", "billboard", sym,
           "https://datacenter-web.eastmoney.com/api/data/v1/get"
           "?reportName=RPT_BILLBOARD_DAILYDETAILS&sortColumns=TRADE_DATE&sortTypes=-1"
           f"&pageSize=20&pageNumber=1&filter=(SECURITY_CODE%3D%22{code6}%22)")
    # also a generic dump (no filter) for shape if 600519 empty
    do_get("eastmoney", "billboard_any", "ANY",
           "https://datacenter-web.eastmoney.com/api/data/v1/get"
           "?reportName=RPT_BILLBOARD_DAILYDETAILS&sortColumns=TRADE_DATE&sortTypes=-1"
           "&pageSize=10&pageNumber=1")
    # Macro GDP
    do_get("eastmoney", "gdp", "MACRO",
           "https://datacenter-web.eastmoney.com/api/data/v1/get"
           "?reportName=RPT_ECONOMY_GDP&columns=ALL&pageSize=10&pageNumber=1"
           "&sortColumns=REPORT_DATE&sortTypes=-1")


# ---------------------------------------------------------------------------
# 5. CNINFO announcements + PDF head
# ---------------------------------------------------------------------------

def cninfo():
    sym = "SH600519"
    url = "http://www.cninfo.com.cn/new/hisAnnouncement/query"
    headers = {
        "User-Agent": UA,
        "Content-Type": "application/x-www-form-urlencoded",
        "Accept-Encoding": "gzip, deflate",
        "Referer": "http://www.cninfo.com.cn/new/commonUrl/pageOfSearch?url=disclosure/list/search",
    }
    body = ("stock=600519,gssh0600519&category=category_ndbg_szsh&"
            "pageNum=1&pageSize=5&column=sse&tabName=fulltext")
    r = do_post("cninfo", "announce_list", sym, url, data=body, headers=headers)
    adjunct = None
    if r is not None and r.ok:
        try:
            payload = r.json()
            anns = payload.get("announcements") or []
            if anns:
                adjunct = anns[0].get("adjunctUrl")
        except Exception as ex:
            record("cninfo", "announce_parse", sym, False, None, "(inline)", f"json parse failed: {ex}")
    if adjunct:
        pdf_url = "http://static.cninfo.com.cn/" + adjunct
        try:
            r2 = requests.get(pdf_url, headers={
                "User-Agent": UA,
                "Accept-Encoding": "gzip, deflate",
                "Range": "bytes=0-1023",
            }, timeout=TIMEOUT, stream=True)
            head = r2.raw.read(1024, decode_content=True)
            write_bytes(f"cninfo_pdf_head_{sym}.bin", head)
            record("cninfo", "pdf_head", sym, r2.ok, r2.status_code,
                   f"cninfo_pdf_head_{sym}.bin",
                   f"len={len(head)}; magic={head[:8]!r}; content-type={r2.headers.get('Content-Type','')}; "
                   f"accept-ranges={r2.headers.get('Accept-Ranges','')}; content-range={r2.headers.get('Content-Range','')}; "
                   f"adjunct={adjunct}")
        except Exception as ex:
            record("cninfo", "pdf_head", sym, False, None, "n/a", f"pdf fetch failed: {ex}")
    else:
        record("cninfo", "pdf_head", sym, False, None, "n/a", "no adjunctUrl from announce_list")


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    t0 = time.time()
    print("[probe] realtime ...")
    realtime()
    print("[probe] kline ...")
    kline()
    print("[probe] financial ...")
    financial()
    print("[probe] deep ...")
    deep()
    print("[probe] cninfo ...")
    cninfo()

    write_text("_summary.json", json.dumps(SUMMARY, ensure_ascii=False, indent=2))
    ok = sum(1 for x in SUMMARY if x["ok"])
    fail = len(SUMMARY) - ok
    print(f"\n[probe] done in {time.time()-t0:.1f}s. total={len(SUMMARY)} ok={ok} fail={fail}")
    print(f"[probe] raw dir: {RAW}")
    if fail:
        print("[probe] failed entries:")
        for x in SUMMARY:
            if not x["ok"]:
                print(f"  - {x['source']}/{x['api']}/{x['symbol']} -> {x['file']} ({x['note']})")


if __name__ == "__main__":
    main()
