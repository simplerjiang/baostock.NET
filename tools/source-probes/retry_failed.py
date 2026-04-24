# -*- coding: utf-8 -*-
"""Retry the entries that failed in the first probe pass."""
from __future__ import annotations
import json, time
from pathlib import Path
import requests
from probe_v120 import do_get, SUMMARY, RAW, write_text, UA  # type: ignore

# Retry helpers ------------------------------------------------------------

def retry_get(source, api, sym, url, headers=None, attempts=4, sleep=1.5,
              filename=None, encoding=None):
    fn = filename or f"{source}_{api}_{sym}.txt"
    last_err = None
    for i in range(attempts):
        try:
            r = requests.get(url, headers=headers or {"User-Agent": UA}, timeout=20)
            if encoding:
                r.encoding = encoding
            else:
                if not r.encoding or r.encoding.lower() == "iso-8859-1":
                    r.encoding = r.apparent_encoding or "utf-8"
            if r.status_code >= 500 or "502 Bad Gateway" in r.text[:200]:
                last_err = f"HTTP {r.status_code} body[:80]={r.text[:80]!r}"
                time.sleep(sleep * (i + 1))
                continue
            (RAW / fn).write_text(r.text, encoding="utf-8")
            print(f"  OK {source}/{api}/{sym} attempt={i+1} status={r.status_code} len={len(r.text)}")
            return True
        except Exception as ex:
            last_err = f"{type(ex).__name__}: {ex}"
            time.sleep(sleep * (i + 1))
    (RAW / fn).write_text(f"RETRY FAILED after {attempts} attempts. Last: {last_err}", encoding="utf-8")
    print(f"  FAIL {source}/{api}/{sym} -> {last_err}")
    return False


print("[retry] eastmoney realtime ...")
for sym in ("SH600519", "SZ000001"):
    secid = ("1." if sym.startswith("SH") else "0.") + sym[2:]
    em_fields = ("f43,f44,f45,f46,f60,f47,f48,f49,f50,f51,f52,f57,f58,"
                 "f168,f169,f170,f171,f47,f48,f86,f117,f162,f152,f167,"
                 "f164,f163,f116,f60,f45,f44,f43,f46,f51,f52,f191,f192")
    retry_get("eastmoney", "realtime", sym,
              f"https://push2.eastmoney.com/api/qt/stock/get?secid={secid}&fields={em_fields}",
              headers={
                  "User-Agent": UA,
                  "Referer": "https://quote.eastmoney.com/",
                  "Accept": "*/*",
              })

print("[retry] eastmoney kline ...")
for sym in ("SH600519", "SZ000001"):
    secid = ("1." if sym.startswith("SH") else "0.") + sym[2:]
    retry_get("eastmoney", "kline_day", sym,
              f"https://push2his.eastmoney.com/api/qt/stock/kline/get"
              f"?secid={secid}&klt=101&fqt=1&lmt=30"
              f"&fields1=f1,f2,f3,f4,f5,f6"
              f"&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61",
              headers={"User-Agent": UA, "Referer": "https://quote.eastmoney.com/"})
    retry_get("eastmoney", "kline_5m", sym,
              f"https://push2his.eastmoney.com/api/qt/stock/kline/get"
              f"?secid={secid}&klt=5&fqt=1&lmt=30"
              f"&fields1=f1,f2,f3,f4,f5,f6"
              f"&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61",
              headers={"User-Agent": UA, "Referer": "https://quote.eastmoney.com/"})

print("[retry] tencent 5m kline (try alt hosts) ...")
for sym, low in (("SH600519", "sh600519"), ("SZ000001", "sz000001")):
    # Try multiple hosts
    for host in ("web.ifzq.gtimg.cn", "ifzq.gtimg.cn"):
        ok = retry_get("tencent", "kline_5m", sym,
                       f"https://{host}/appstock/app/kline/mkline?param={low},m5,,30",
                       attempts=3)
        if ok:
            break
