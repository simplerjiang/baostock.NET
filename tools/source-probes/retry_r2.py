# -*- coding: utf-8 -*-
"""Round 2 retry: kline rc=102 means request rejected. Add ut param and end/beg.
Also retry SH600519 realtime via http and alternate push2 hosts."""
from __future__ import annotations
import time, requests
from pathlib import Path

RAW = Path(__file__).resolve().parent / "raw"
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
      "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36")
UT = "fa5fd1943c7b386f172d6893dbfba10b"  # public anonymous token used by EM

def save(name, text):
    (RAW / name).write_text(text, encoding="utf-8")

def hit(label, url, headers=None, attempts=3, sleep=1.0):
    last = None
    for i in range(attempts):
        try:
            r = requests.get(url, headers=headers or {"User-Agent": UA}, timeout=20)
            r.encoding = r.apparent_encoding or "utf-8"
            short = r.text[:120]
            ok = r.ok and '"rc":0' in r.text or "qfqday" in r.text or "data" in r.text.lower()
            print(f"  [{label}] try{i+1} status={r.status_code} len={len(r.text)} preview={short!r}")
            if r.ok and ('"rc":0' in r.text or len(r.text) > 200):
                return r.text
            last = f"status={r.status_code} body={short!r}"
        except Exception as ex:
            last = f"{type(ex).__name__}: {ex}"
            print(f"  [{label}] try{i+1} EXC {last}")
        time.sleep(sleep * (i + 1))
    return last

# Round 2: EM realtime SH600519 — try http, alternate hosts
print("[r2] EM realtime SH600519")
em_fields = ("f43,f44,f45,f46,f60,f47,f48,f49,f50,f51,f52,f57,f58,"
             "f168,f169,f170,f171,f86,f117,f162,f152,f167,f164,f163,"
             "f116,f191,f192")
for host_scheme in ("https://push2.eastmoney.com", "https://push2delay.eastmoney.com",
                    "http://push2.eastmoney.com"):
    text = hit("realtime SH",
               f"{host_scheme}/api/qt/stock/get?secid=1.600519&fields={em_fields}&ut={UT}",
               headers={"User-Agent": UA, "Referer": "https://quote.eastmoney.com/"})
    if isinstance(text, str) and '"rc":0' in text:
        save("eastmoney_realtime_SH600519.txt", text)
        print("  -> saved")
        break

# Round 2: EM kline — add ut, end, beg
print("[r2] EM kline")
for sym in ("SH600519", "SZ000001"):
    secid = ("1." if sym.startswith("SH") else "0.") + sym[2:]
    for klt, label in ((101, "kline_day"), (5, "kline_5m")):
        text = hit(f"{label} {sym}",
                   f"https://push2his.eastmoney.com/api/qt/stock/kline/get"
                   f"?secid={secid}&ut={UT}&klt={klt}&fqt=1&end=20500101&lmt=30"
                   f"&fields1=f1,f2,f3,f4,f5,f6,f7,f8,f9,f10,f11,f12,f13"
                   f"&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61",
                   headers={"User-Agent": UA, "Referer": "https://quote.eastmoney.com/"})
        if isinstance(text, str) and '"rc":0' in text:
            save(f"eastmoney_{label}_{sym}.txt", text)
            print("  -> saved")
