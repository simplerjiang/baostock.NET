"""Capture raw socket request/response bytes for every baostock query API.

Outputs into tests/Fixtures/<api_name>/{request.bin,response.bin,meta.txt}.
Skipped APIs are recorded in tests/Fixtures/_skipped.md.
"""
from __future__ import annotations

import os
import socket
import sys
import time
import traceback
from pathlib import Path

import baostock as bs

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "tests" / "Fixtures"
OUT.mkdir(parents=True, exist_ok=True)
SKIPPED_PATH = OUT / "_skipped.md"

_state = {"name": None, "sent": b"", "recv": b""}

_orig_send = socket.socket.send
_orig_sendall = socket.socket.sendall
_orig_recv = socket.socket.recv


def _send(self, data, *a, **kw):
    if _state["name"]:
        try:
            _state["sent"] += bytes(data)
        except Exception:
            pass
    return _orig_send(self, data, *a, **kw)


def _sendall(self, data, *a, **kw):
    if _state["name"]:
        try:
            _state["sent"] += bytes(data)
        except Exception:
            pass
    return _orig_sendall(self, data, *a, **kw)


def _recv(self, n, *a, **kw):
    chunk = _orig_recv(self, n, *a, **kw)
    if _state["name"] and chunk:
        _state["recv"] += chunk
    return chunk


socket.socket.send = _send
socket.socket.sendall = _sendall
socket.socket.recv = _recv


_skipped: list[tuple[str, str]] = []
_errors: list[tuple[str, str, str]] = []  # (name, error_code, error_msg)
_succeeded: list[str] = []


def _drain(rs):
    """Pull every row of a ResultSet so all frames are received."""
    if rs is None:
        return 0
    if not hasattr(rs, "next"):
        return 0
    rows = 0
    try:
        while rs.error_code == "0" and rs.next():
            rs.get_row_data()
            rows += 1
    except Exception:
        pass
    return rows


def capture(name: str, fn):
    print(f"[capture] {name} ...", flush=True)
    _state.update(name=name, sent=b"", recv=b"")
    rs = None
    err = None
    rows = 0
    try:
        rs = fn()
        rows = _drain(rs)
    except Exception as exc:  # noqa: BLE001
        err = exc
        traceback.print_exc()
    finally:
        # snapshot bytes before disabling capture
        sent = _state["sent"]
        recv = _state["recv"]
        _state["name"] = None

    if err is not None or not sent:
        reason = f"exception: {err!r}" if err else "no bytes captured"
        _skipped.append((name, reason))
        print(f"  -> SKIPPED ({reason})", flush=True)
        time.sleep(0.4)
        return

    d = OUT / name
    d.mkdir(parents=True, exist_ok=True)
    (d / "request.bin").write_bytes(sent)
    (d / "response.bin").write_bytes(recv)
    error_code = getattr(rs, "error_code", "")
    error_msg = getattr(rs, "error_msg", "")
    fields = getattr(rs, "fields", [])
    (d / "meta.txt").write_text(
        "name={}\n"
        "error_code={}\n"
        "error_msg={}\n"
        "fields={}\n"
        "rows={}\n"
        "req_bytes={}\n"
        "resp_bytes={}\n".format(
            name, error_code, error_msg, fields, rows, len(sent), len(recv)
        ),
        encoding="utf-8",
    )
    _succeeded.append(name)
    if str(error_code) != "0":
        _errors.append((name, str(error_code), str(error_msg)))
    print(
        f"  -> OK rows={rows} req={len(sent)}B resp={len(recv)}B "
        f"err={error_code}",
        flush=True,
    )
    time.sleep(0.5)


def maybe(api_name: str):
    """Return getattr(bs, api_name) or None if missing."""
    fn = getattr(bs, api_name, None)
    if fn is None:
        _skipped.append((api_name, "not exported by baostock package"))
        print(f"[capture] {api_name} -> SKIPPED (not in bs)", flush=True)
    return fn


def main() -> int:
    t0 = time.time()

    # ---- session: login ----
    capture("login", lambda: bs.login())

    code = "sh.600000"

    # ---- K line ----
    kfields = (
        "date,code,open,high,low,close,preclose,volume,amount,"
        "adjustflag,turn,tradestatus,pctChg,isST"
    )
    capture(
        "query_history_k_data_plus",
        lambda: bs.query_history_k_data_plus(
            code,
            kfields,
            start_date="2024-01-01",
            end_date="2024-01-31",
            frequency="d",
            adjustflag="2",
        ),
    )

    # ---- sectors (exported) ----
    capture("query_stock_industry", lambda: bs.query_stock_industry(code=code))
    capture("query_hs300_stocks", lambda: bs.query_hs300_stocks())
    capture("query_sz50_stocks", lambda: bs.query_sz50_stocks())
    capture("query_zz500_stocks", lambda: bs.query_zz500_stocks())

    # ---- sectors (candidates) ----
    sector_date = "2024-01-02"
    for api in [
        "query_terminated_stocks",
        "query_suspended_stocks",
        "query_st_stocks",
        "query_starst_stocks",
        "query_ame_stocks",
        "query_gem_stocks",
        "query_shhk_stocks",
        "query_szhk_stocks",
        "query_stocks_in_risk",
    ]:
        fn = maybe(api)
        if fn:
            capture(api, lambda fn=fn: fn(date=sector_date))
    for api in ["query_stock_concept", "query_stock_area"]:
        fn = maybe(api)
        if fn:
            capture(api, lambda fn=fn: fn(code=code, date=sector_date))

    # ---- quarterly financials ----
    year = 2023
    quarter = 2
    capture(
        "query_dividend_data",
        lambda: bs.query_dividend_data(code=code, year=str(year), yearType="report"),
    )
    capture(
        "query_adjust_factor",
        lambda: bs.query_adjust_factor(
            code=code, start_date="2023-01-01", end_date="2023-12-31"
        ),
    )
    for api in [
        "query_profit_data",
        "query_operation_data",
        "query_growth_data",
        "query_dupont_data",
        "query_balance_data",
        "query_cash_flow_data",
    ]:
        fn = getattr(bs, api)
        capture(api, lambda fn=fn: fn(code=code, year=year, quarter=quarter))

    # ---- corporate announcements ----
    capture(
        "query_performance_express_report",
        lambda: bs.query_performance_express_report(
            code, start_date="2023-01-01", end_date="2023-12-31"
        ),
    )
    capture(
        "query_forecast_report",
        lambda: bs.query_forecast_report(
            code, start_date="2023-01-01", end_date="2023-12-31"
        ),
    )

    # ---- metadata ----
    capture(
        "query_trade_dates",
        lambda: bs.query_trade_dates(start_date="2024-01-01", end_date="2024-01-10"),
    )
    capture("query_all_stock", lambda: bs.query_all_stock(day="2024-01-02"))
    capture("query_stock_basic", lambda: bs.query_stock_basic(code=code))

    # ---- macro (exported) ----
    capture(
        "query_deposit_rate_data",
        lambda: bs.query_deposit_rate_data(
            start_date="2010-01-01", end_date="2015-12-31"
        ),
    )
    capture(
        "query_loan_rate_data",
        lambda: bs.query_loan_rate_data(
            start_date="2010-01-01", end_date="2015-12-31"
        ),
    )
    capture(
        "query_required_reserve_ratio_data",
        lambda: bs.query_required_reserve_ratio_data(
            start_date="2010-01-01", end_date="2015-12-31", yearType="0"
        ),
    )
    capture(
        "query_money_supply_data_month",
        lambda: bs.query_money_supply_data_month(
            start_date="2023-01", end_date="2023-12"
        ),
    )
    capture(
        "query_money_supply_data_year",
        lambda: bs.query_money_supply_data_year(
            start_date="2020", end_date="2023"
        ),
    )

    # ---- macro (candidates) ----
    for api in ["query_cpi_data", "query_ppi_data", "query_pmi_data"]:
        fn = maybe(api)
        if fn:
            capture(
                api,
                lambda fn=fn: fn(start_date="2020-01", end_date="2023-12"),
            )

    # ---- session: logout ----
    capture("logout", lambda: bs.logout())

    elapsed = time.time() - t0

    # write skipped report
    lines = [
        "# Skipped / failed APIs",
        "",
        f"_Captured at: {time.strftime('%Y-%m-%d %H:%M:%S')}_",
        "",
    ]
    if _skipped:
        for name, reason in _skipped:
            lines.append(f"- **{name}** — {reason}")
    else:
        lines.append("_(none)_")
    SKIPPED_PATH.write_text("\n".join(lines) + "\n", encoding="utf-8")

    print()
    print("=" * 60)
    print(f"succeeded: {len(_succeeded)}")
    print(f"skipped:   {len(_skipped)}")
    print(f"errors(error_code!=0): {len(_errors)}")
    for name, ec, em in _errors:
        print(f"  - {name}: code={ec} msg={em}")
    print(f"elapsed: {elapsed:.2f}s")
    return 0


if __name__ == "__main__":
    sys.exit(main())
