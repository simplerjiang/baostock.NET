"""Capture 5min and 60min K-line fixtures using the same monkey-patch approach."""
from __future__ import annotations

import socket
import time
from pathlib import Path

import baostock as bs

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "tests" / "Fixtures"

_state = {"name": None, "sent": b"", "recv": b""}
_orig_send = socket.socket.send
_orig_sendall = socket.socket.sendall
_orig_recv = socket.socket.recv


def _send(self, data, *a, **kw):
    if _state["name"]:
        _state["sent"] += bytes(data)
    return _orig_send(self, data, *a, **kw)


def _sendall(self, data, *a, **kw):
    if _state["name"]:
        _state["sent"] += bytes(data)
    return _orig_sendall(self, data, *a, **kw)


def _recv(self, n, *a, **kw):
    chunk = _orig_recv(self, n, *a, **kw)
    if _state["name"] and chunk:
        _state["recv"] += chunk
    return chunk


socket.socket.send = _send
socket.socket.sendall = _sendall
socket.socket.recv = _recv

FIELDS = "date,time,code,open,high,low,close,volume,amount,adjustflag"


def capture_kline(name: str, frequency: str):
    print(f"\n{'='*60}")
    print(f"[capture] {name} (frequency={frequency})")
    print(f"{'='*60}")

    _state.update(name=name, sent=b"", recv=b"")
    rs = bs.query_history_k_data_plus(
        "sh.600000",
        FIELDS,
        start_date="2024-01-02",
        end_date="2024-01-03",
        frequency=frequency,
        adjustflag="2",
    )

    rows = []
    while rs.error_code == "0" and rs.next():
        rows.append(rs.get_row_data())

    sent = _state["sent"]
    recv = _state["recv"]
    _state["name"] = None

    # Print details
    print(f"error_code: {rs.error_code}")
    print(f"error_msg:  {rs.error_msg}")
    print(f"fields:     {rs.fields}")
    print(f"row count:  {len(rows)}")
    print(f"req bytes:  {len(sent)}")
    print(f"resp bytes: {len(recv)}")
    print(f"\nFirst 5 rows:")
    for i, r in enumerate(rows[:5]):
        print(f"  [{i}] {r}")

    # Save fixture
    d = OUT / name
    d.mkdir(parents=True, exist_ok=True)
    (d / "request.bin").write_bytes(sent)
    (d / "response.bin").write_bytes(recv)

    first_row_str = str(rows[0]) if rows else "(none)"
    (d / "meta.txt").write_text(
        f"name={name}\n"
        f"error_code={rs.error_code}\n"
        f"error_msg={rs.error_msg}\n"
        f"fields={rs.fields}\n"
        f"rows={len(rows)}\n"
        f"req_bytes={len(sent)}\n"
        f"resp_bytes={len(recv)}\n"
        f"first_row={first_row_str}\n",
        encoding="utf-8",
    )
    print(f"\nSaved to {d}")
    return rs, rows


def main():
    lg = bs.login()
    print(f"login: error_code={lg.error_code}, error_msg={lg.error_msg}")

    rs5, rows5 = capture_kline("query_history_k_data_plus_5min", "5")
    time.sleep(0.5)
    rs60, rows60 = capture_kline("query_history_k_data_plus_60min", "60")

    bs.logout()

    # Summary
    print(f"\n{'='*60}")
    print("SUMMARY")
    print(f"{'='*60}")
    print(f"\n5min fields:  {rs5.fields}")
    print(f"60min fields: {rs60.fields}")
    print(f"\n5min rows:  {len(rows5)}")
    print(f"60min rows: {len(rows60)}")
    print(f"\n5min first 3 rows:")
    for r in rows5[:3]:
        print(f"  {r}")
    print(f"\n60min first 3 rows:")
    for r in rows60[:3]:
        print(f"  {r}")


if __name__ == "__main__":
    main()
