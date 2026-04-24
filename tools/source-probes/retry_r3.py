# -*- coding: utf-8 -*-
"""Round 3: bank companyType=3 for SZ000001 financials; billboard columns=ALL."""
from __future__ import annotations
import time, requests
from pathlib import Path

RAW = Path(__file__).resolve().parent / "raw"
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
      "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36")

def hit(url, headers=None, attempts=3, sleep=1.0):
    last = None
    for i in range(attempts):
        try:
            r = requests.get(url, headers=headers or {"User-Agent": UA}, timeout=20)
            r.encoding = r.apparent_encoding or "utf-8"
            print(f"  try{i+1} status={r.status_code} len={len(r.text)} preview={r.text[:120]!r}")
            if r.ok and len(r.text) > 200:
                return r.text
            last = f"status={r.status_code} body={r.text[:80]!r}"
        except Exception as ex:
            last = f"{type(ex).__name__}: {ex}"
            print(f"  try{i+1} EXC {last}")
        time.sleep(sleep * (i + 1))
    return last

# 1) SZ000001 financials with companyType=3 (bank)
print("[r3] SZ000001 financials companyType=3")
sym = "SZ000001"
for api, ep in (("zcfzb", "zcfzbAjaxNew"),
                ("lrb", "lrbAjaxNew"),
                ("xjllb", "xjllbAjaxNew")):
    text = hit(f"https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/{ep}"
               f"?companyType=3&reportDateType=0&reportType=1&dates=2024-12-31&code={sym}",
               headers={"User-Agent": UA, "Referer": f"https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/Index?type=web&code={sym}"})
    if isinstance(text, str) and len(text) > 200:
        (RAW / f"eastmoney_{api}_{sym}.txt").write_text(text, encoding="utf-8")
        print("  -> saved")
for api, ep in (("zcfzb_dates", "zcfzbDateAjaxNew"),
                ("xjllb_dates", "xjllbDateAjaxNew")):
    text = hit(f"https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/{ep}"
               f"?companyType=3&reportDateType=0&code={sym}",
               headers={"User-Agent": UA, "Referer": f"https://emweb.securities.eastmoney.com/PC_HSF10/NewFinanceAnalysis/Index?type=web&code={sym}"})
    if isinstance(text, str) and len(text) > 200:
        (RAW / f"eastmoney_{api}_{sym}.txt").write_text(text, encoding="utf-8")
        print("  -> saved")

# 2) Billboard with columns=ALL
print("[r3] Billboard with columns=ALL")
text = hit("https://datacenter-web.eastmoney.com/api/data/v1/get"
           "?reportName=RPT_BILLBOARD_DAILYDETAILS&columns=ALL"
           "&sortColumns=TRADE_DATE&sortTypes=-1&pageSize=20&pageNumber=1"
           "&filter=(SECURITY_CODE%3D%22600519%22)")
if isinstance(text, str):
    (RAW / "eastmoney_billboard_SH600519.txt").write_text(text, encoding="utf-8")

text = hit("https://datacenter-web.eastmoney.com/api/data/v1/get"
           "?reportName=RPT_BILLBOARD_DAILYDETAILS&columns=ALL"
           "&sortColumns=TRADE_DATE&sortTypes=-1&pageSize=10&pageNumber=1")
if isinstance(text, str):
    (RAW / "eastmoney_billboard_any_ANY.txt").write_text(text, encoding="utf-8")
