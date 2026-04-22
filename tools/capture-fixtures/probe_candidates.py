"""Probe baostock server with raw socket frames for 14 candidate APIs.

Bypasses the baostock pip package and constructs frames directly:
    HEADER (21B) = "00.9.10" + "\x01" + MSG_TYPE(2) + "\x01" + body_length(10, zero-padded)
    FRAME        = HEADER + body + "\x01" + str(zlib.crc32(HEADER+body)) + "\n"

Body parameter format (mirrors organize_msg_body in baostock-python):
    method_name + "\x01" + user_id + "\x01" + "1" + "\x01" + "10000" + "\x01" + ...args

Output: tests/Fixtures/_candidates/<api_name>/{request.bin,response.bin,meta.txt}
"""
from __future__ import annotations

import socket
import sys
import time
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "tests" / "Fixtures" / "_candidates"
OUT.mkdir(parents=True, exist_ok=True)

HOST = "public-api.baostock.com"
PORT = 10030
CLIENT_VERSION = "00.9.10"
SPLIT = "\x01"
HEADER_LEN = 21
PER_PAGE = "10000"
RECV_TIMEOUT = 5.0
SLEEP = 0.3

# COMPRESSED_MESSAGE_TYPE_TUPLE = ("96",)  # only k_data_plus response
COMPRESSED_TYPES = {"96"}


def build_frame(msg_type: str, body: str) -> bytes:
    header = (
        CLIENT_VERSION
        + SPLIT
        + msg_type
        + SPLIT
        + str(len(body)).zfill(10)
    )
    head_body = header + body
    crc = zlib.crc32(head_body.encode("utf-8"))
    frame = head_body + SPLIT + str(crc) + "\n"
    return frame.encode("utf-8")


def recv_exact(sock: socket.socket, n: int) -> bytes:
    buf = b""
    while len(buf) < n:
        chunk = sock.recv(n - len(buf))
        if not chunk:
            return buf
        buf += chunk
    return buf


def recv_response(sock: socket.socket) -> bytes:
    """Read one response: 21B header + body_length bytes + trailer until \\n."""
    header = recv_exact(sock, HEADER_LEN)
    if len(header) < HEADER_LEN:
        return header
    parts = header.decode("utf-8", errors="replace").split(SPLIT)
    if len(parts) < 3:
        # malformed - drain a bit then return
        try:
            extra = sock.recv(8192)
        except Exception:
            extra = b""
        return header + extra
    msg_type = parts[1]
    try:
        body_length = int(parts[2])
    except ValueError:
        body_length = 0
    body = recv_exact(sock, body_length)
    # Trailer: "\x01" + crc + "\n"  (compressed responses use "<![CDATA[]]>\n")
    suffix_target = b"<![CDATA[]]>\n" if msg_type in COMPRESSED_TYPES else b"\n"
    trailer = b""
    deadline = time.time() + RECV_TIMEOUT
    while time.time() < deadline:
        try:
            chunk = sock.recv(4096)
        except socket.timeout:
            break
        if not chunk:
            break
        trailer += chunk
        if trailer.endswith(suffix_target):
            break
        # safety: cap trailer
        if len(trailer) > 65536:
            break
    return header + body + trailer


def parse_error(resp: bytes) -> tuple[str, str, str]:
    """Return (msg_type, error_code, error_msg) from a response."""
    if len(resp) < HEADER_LEN:
        return ("", "", "")
    head = resp[:HEADER_LEN].decode("utf-8", errors="replace")
    head_parts = head.split(SPLIT)
    msg_type = head_parts[1] if len(head_parts) > 1 else ""
    body_length = 0
    if len(head_parts) > 2:
        try:
            body_length = int(head_parts[2])
        except ValueError:
            body_length = 0
    body = resp[HEADER_LEN : HEADER_LEN + body_length].decode(
        "utf-8", errors="replace"
    )
    body_parts = body.split(SPLIT)
    err_code = body_parts[0] if len(body_parts) > 0 else ""
    err_msg = body_parts[1] if len(body_parts) > 1 else ""
    return (msg_type, err_code, err_msg)


def login_anonymous(sock: socket.socket) -> tuple[bytes, bytes, str]:
    body = "login" + SPLIT + "anonymous" + SPLIT + "123456" + SPLIT + "0"
    req = build_frame("00", body)
    sock.sendall(req)
    resp = recv_response(sock)
    _, ec, em = parse_error(resp)
    return req, resp, em


def write_meta(out_dir: Path, **fields) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    lines = [f"{k}={v}" for k, v in fields.items()]
    (out_dir / "meta.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")


def probe(
    sock: socket.socket,
    name: str,
    msg_type: str,
    body: str,
    out_dir: Path,
) -> dict:
    """Send one probe frame, save artifacts, return result summary."""
    req = build_frame(msg_type, body)
    err_label = "ok"
    socket_dead = False
    resp = b""
    try:
        sock.sendall(req)
        resp = recv_response(sock)
    except (ConnectionResetError, BrokenPipeError, OSError) as exc:
        err_label = f"socket_error: {exc!r}"
        socket_dead = True

    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "request.bin").write_bytes(req)
    (out_dir / "response.bin").write_bytes(resp)

    resp_msg_type, err_code, err_msg = parse_error(resp)
    body_preview = ""
    if len(resp) > HEADER_LEN:
        body_text = resp[HEADER_LEN:].decode("utf-8", errors="replace")
        body_preview = body_text[:200].replace("\x01", "|").replace("\n", "\\n")

    write_meta(
        out_dir,
        name=name,
        request_msg_type=msg_type,
        response_msg_type=resp_msg_type,
        error_code=err_code,
        error_msg=err_msg,
        req_bytes=len(req),
        resp_bytes=len(resp),
        socket_status=err_label,
        body_preview=body_preview,
    )
    return {
        "name": name,
        "msg_type": msg_type,
        "resp_msg_type": resp_msg_type,
        "error_code": err_code,
        "error_msg": err_msg,
        "resp_bytes": len(resp),
        "socket_dead": socket_dead,
        "body": body,
    }


def make_body_simple(method: str, user_id: str) -> str:
    """method,user_id,1,10000 — no extra args."""
    return SPLIT.join([method, user_id, "1", PER_PAGE])


def make_body_with(method: str, user_id: str, *args: str) -> str:
    parts = [method, user_id, "1", PER_PAGE, *args]
    return SPLIT.join(parts)


# ---- 14 candidates: (api_name, msg_type, params for round 2) ----
# Parameter slots come straight from reference/baostock-python sources.
CANDIDATES_DATE = [
    # (api, msg_type)  body slot: ",<date>"
    ("query_terminated_stocks", "67"),
    ("query_suspended_stocks", "69"),
    ("query_st_stocks", "71"),
    ("query_starst_stocks", "73"),
    ("query_ame_stocks", "85"),
    ("query_gem_stocks", "87"),
    ("query_shhk_stocks", "89"),
    ("query_szhk_stocks", "91"),
    ("query_stocks_in_risk", "93"),
]

CANDIDATES_CODE_DATE = [
    # body slot: ",<code>,<date>"
    ("query_stock_concept", "81"),
    ("query_stock_area", "83"),
]

CANDIDATES_DATE_RANGE = [
    # body slot: ",<start>,<end>"
    ("query_cpi_data", "75"),
    ("query_ppi_data", "77"),
    ("query_pmi_data", "79"),
]

ALL_CANDIDATES = (
    [(n, m, ["2024-01-02"]) for (n, m) in CANDIDATES_DATE]
    + [(n, m, ["sh.600000", "2024-01-02"]) for (n, m) in CANDIDATES_CODE_DATE]
    + [(n, m, ["2020-01-01", "2023-12-31"]) for (n, m) in CANDIDATES_DATE_RANGE]
)


def connect() -> socket.socket:
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.settimeout(RECV_TIMEOUT)
    s.connect((HOST, PORT))
    return s


def main() -> int:
    t0 = time.time()
    print(f"[connect] {HOST}:{PORT}", flush=True)
    sock = connect()
    user_id = "anonymous"

    # --- login ---
    login_req, login_resp, login_msg = login_anonymous(sock)
    login_dir = OUT / "_login"
    login_dir.mkdir(parents=True, exist_ok=True)
    (login_dir / "request.bin").write_bytes(login_req)
    (login_dir / "response.bin").write_bytes(login_resp)
    _, lec, lem = parse_error(login_resp)
    write_meta(
        login_dir,
        name="login",
        request_msg_type="00",
        error_code=lec,
        error_msg=lem,
        req_bytes=len(login_req),
        resp_bytes=len(login_resp),
    )
    print(f"[login] error_code={lec} error_msg={lem}", flush=True)
    if lec != "0":
        print("[fatal] login failed; aborting.", flush=True)
        return 2

    # --- Round 1: minimal body (method only, no args) ---
    print("\n========== ROUND 1: minimal body ==========", flush=True)
    round1_results: dict[str, dict] = {}
    reconnects = 0
    for name, msg_type, _args in ALL_CANDIDATES:
        body = make_body_simple(name, user_id)
        out_dir = OUT / name / "round1"
        try:
            res = probe(sock, name, msg_type, body, out_dir)
        except Exception as exc:
            print(f"  [{name}] EXCEPTION {exc!r}", flush=True)
            res = {
                "name": name,
                "msg_type": msg_type,
                "resp_msg_type": "",
                "error_code": "",
                "error_msg": f"exception: {exc!r}",
                "resp_bytes": 0,
                "socket_dead": True,
                "body": body,
            }
        round1_results[name] = res
        ec = res["error_code"]
        em = res["error_msg"]
        rmt = res["resp_msg_type"]
        print(
            f"  [{name}] msg={msg_type} -> resp_msg={rmt} ec={ec} "
            f"em={em[:60]} bytes={res['resp_bytes']}",
            flush=True,
        )
        if res["socket_dead"]:
            print("  -> socket dead, reconnecting + re-login", flush=True)
            try:
                sock.close()
            except Exception:
                pass
            sock = connect()
            login_anonymous(sock)
            reconnects += 1
        time.sleep(SLEEP)

    # --- Round 2: only for APIs whose round1 looked like param error ---
    print("\n========== ROUND 2: with args ==========", flush=True)
    PARAM_ERR_CODES = {
        "10004005",  # 传入参数为空
        "10004006",  # 参数错误
        "10004007",
        "10004008",
        "10004009",
        "10004010",
        "10004011",
        "10004013",
        "10004019",
    }
    round2_results: dict[str, dict] = {}
    for name, msg_type, args in ALL_CANDIDATES:
        r1 = round1_results.get(name, {})
        ec1 = r1.get("error_code", "")
        if ec1 not in PARAM_ERR_CODES:
            continue
        body = make_body_with(name, user_id, *args)
        out_dir = OUT / name / "round2"
        try:
            res = probe(sock, name, msg_type, body, out_dir)
        except Exception as exc:
            print(f"  [{name}] EXCEPTION {exc!r}", flush=True)
            res = {
                "name": name,
                "msg_type": msg_type,
                "resp_msg_type": "",
                "error_code": "",
                "error_msg": f"exception: {exc!r}",
                "resp_bytes": 0,
                "socket_dead": True,
                "body": body,
            }
        round2_results[name] = res
        ec = res["error_code"]
        em = res["error_msg"]
        rmt = res["resp_msg_type"]
        print(
            f"  [{name}] msg={msg_type} -> resp_msg={rmt} ec={ec} "
            f"em={em[:60]} bytes={res['resp_bytes']}",
            flush=True,
        )
        if res["socket_dead"]:
            try:
                sock.close()
            except Exception:
                pass
            sock = connect()
            login_anonymous(sock)
            reconnects += 1
        time.sleep(SLEEP)

    # --- Logout ---
    try:
        now_time = time.strftime("%Y%m%d%H%M%S")
        body = "logout" + SPLIT + user_id + SPLIT + now_time
        sock.sendall(build_frame("02", body))
        recv_response(sock)
        sock.close()
    except Exception:
        pass

    elapsed = time.time() - t0

    # --- Final classification ---
    print("\n" + "=" * 70, flush=True)
    print("CLASSIFICATION", flush=True)
    print("=" * 70, flush=True)

    cls_A = []  # supported (ec=0 OR business error, but server recognised MSG)
    cls_B = []  # not supported (msg_type=04 / connection killed / no response)
    cls_C = []  # known in source but params not figured out

    for name, msg_type, _args in ALL_CANDIDATES:
        r1 = round1_results.get(name, {})
        r2 = round2_results.get(name)
        # pick the better of the two
        chosen = r2 if r2 is not None else r1
        ec = chosen.get("error_code", "")
        rmt = chosen.get("resp_msg_type", "")
        resp_bytes = chosen.get("resp_bytes", 0)
        socket_dead = chosen.get("socket_dead", False)

        bucket = None
        if resp_bytes == 0 or socket_dead:
            bucket = "B"
        elif rmt == "04":
            # check if it's "MSG type unknown" vs other server exception
            em = chosen.get("error_msg", "")
            if "10004020" == ec or "消息" in em or "类型" in em:
                bucket = "B"
            else:
                bucket = "A"  # server-side exception but recognised
        elif ec == "0":
            bucket = "A"
        else:
            # business error: server understood the MSG but rejected our body
            # If even round2 still gives param error, classify C
            if r2 is not None and ec in PARAM_ERR_CODES:
                bucket = "C"
            else:
                bucket = "A"

        line = (
            f"  {name:35s} msg={msg_type} -> resp_msg={rmt or '--':>2} "
            f"ec={ec:>10s} bytes={resp_bytes:>5d}  body={chosen.get('body','')!r}"
        )
        if bucket == "A":
            cls_A.append((name, msg_type, chosen))
        elif bucket == "B":
            cls_B.append((name, msg_type, chosen))
        else:
            cls_C.append((name, msg_type, chosen))
        print(f"[{bucket}]" + line, flush=True)

    print("\n--- A. Server still supports MSG ---", flush=True)
    for n, m, c in cls_A:
        print(
            f"  {n} (MSG={m}) ec={c['error_code']} em={c['error_msg'][:60]}",
            flush=True,
        )
    print("\n--- B. Server does NOT recognise MSG ---", flush=True)
    for n, m, c in cls_B:
        print(f"  {n} (MSG={m}) -> probably retired", flush=True)
    print("\n--- C. Source defines it but params not nailed ---", flush=True)
    for n, m, c in cls_C:
        print(
            f"  {n} (MSG={m}) ec={c['error_code']} em={c['error_msg'][:60]}",
            flush=True,
        )

    print(
        f"\nreconnects: {reconnects}    elapsed: {elapsed:.2f}s",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
