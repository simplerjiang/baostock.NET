"use strict";

const state = {
  endpoints: [],
  current: null,
  loadTargets: [],
  loadRunning: false,
};

document.addEventListener("DOMContentLoaded", async () => {
  bindSession();
  bindTabs();
  bindLoadTest();
  await refreshStatus();
  await loadEndpoints();
  await loadLoadTargets();
});

// ── session ─────────────────────────────────────────
function bindSession() {
  document.getElementById("login-btn").addEventListener("click", async () => {
    const userId = document.getElementById("userId").value;
    const password = document.getElementById("password").value;
    const r = await postJson("/api/session/login", { userId, password });
    alert("Login: " + JSON.stringify(r));
    await refreshStatus();
  });
  document.getElementById("logout-btn").addEventListener("click", async () => {
    const r = await postJson("/api/session/logout", {});
    alert("Logout: " + JSON.stringify(r));
    await refreshStatus();
  });
  document.getElementById("status-btn").addEventListener("click", refreshStatus);
}

async function refreshStatus() {
  try {
    const r = await fetch("/api/session/status").then(x => x.json());
    const el = document.getElementById("session-status");
    if (r.isLoggedIn) {
      el.textContent = `已登录 (${r.userId})`;
      el.className = "ok";
    } else {
      el.textContent = "未登录";
      el.className = "no";
    }
    const dot = document.getElementById("socket-indicator");
    if (dot) {
      if (r.isSocketConnected) {
        dot.className = "socket-dot socket-connected";
        dot.title = "TCP socket: connected";
      } else {
        dot.className = "socket-dot socket-disconnected";
        dot.title = "TCP socket: disconnected";
      }
    }
  } catch (e) {
    document.getElementById("session-status").textContent = "状态查询失败";
    const dot = document.getElementById("socket-indicator");
    if (dot) {
      dot.className = "socket-dot socket-disconnected";
      dot.title = "TCP socket: unknown (status query failed)";
    }
  }
}

// ── endpoints list ─────────────────────────────────
async function loadEndpoints() {
  try {
    state.endpoints = await fetch("/api/meta/endpoints").then(r => r.json());
  } catch (e) {
    document.getElementById("endpoint-list").innerHTML =
      `<div class="loading">加载失败: ${e.message}</div>`;
    return;
  }
  renderEndpointList();
}

function renderEndpointList() {
  const aside = document.getElementById("endpoint-list");
  aside.innerHTML = "";

  const groups = new Map();
  for (const ep of state.endpoints) {
    if (!groups.has(ep.group)) groups.set(ep.group, []);
    groups.get(ep.group).push(ep);
  }

  for (const [group, eps] of groups) {
    const wrap = document.createElement("div");
    wrap.className = "group";

    const title = document.createElement("div");
    title.className = "group-title";
    title.textContent = `${group} (${eps.length})`;
    wrap.appendChild(title);

    const items = document.createElement("div");
    items.className = "group-items";
    title.addEventListener("click", () => items.classList.toggle("collapsed"));

    for (const ep of eps) {
      const item = document.createElement("div");
      item.className = "endpoint-item proto-" + (ep.protocol || "unknown");
      const badge = document.createElement("span");
      badge.className = "proto-badge proto-badge-" + (ep.protocol || "unknown");
      badge.textContent = protocolBadgeText(ep.protocol);
      item.appendChild(badge);
      item.appendChild(document.createTextNode(" " + ep.name));
      item.dataset.path = ep.path;
      item.addEventListener("click", () => selectEndpoint(ep, item));
      items.appendChild(item);
    }
    wrap.appendChild(items);
    aside.appendChild(wrap);
  }
}

function selectEndpoint(ep, itemEl) {
  state.current = ep;
  document.querySelectorAll(".endpoint-item.active").forEach(e => e.classList.remove("active"));
  if (itemEl) itemEl.classList.add("active");

  document.getElementById("ep-name").textContent = `${ep.group} / ${ep.name}`;
  document.getElementById("ep-desc").textContent = ep.description;
  document.getElementById("ep-path").textContent = `POST ${ep.path}`;

  // 协议警告 banner
  const banner = document.getElementById("ep-banner");
  const proto = ep.protocol || "unknown";
  if (proto === "tcp") {
    banner.hidden = false;
    banner.className = "banner banner-warn";
    banner.textContent = "⚠️ baostock TCP 端点：使用单条共享长连接，非线程安全。不要在压测面板对此端点用 concurrency > 1。";
  } else if (proto === "internal") {
    banner.hidden = false;
    banner.className = "banner banner-info";
    banner.textContent = "内部端点（meta/loadtest），主要供 UI 自身使用";
  } else {
    banner.hidden = true;
    banner.className = "";
    banner.textContent = "";
  }

  const form = document.getElementById("ep-form");
  form.innerHTML = "";
  for (const f of ep.fields) {
    const label = document.createElement("label");
    label.textContent = f.name + (f.required ? " *" : "");
    label.title = f.type;
    form.appendChild(label);

    let input;
    if (f.type === "enum" && Array.isArray(f.options)) {
      input = document.createElement("select");
      for (const opt of f.options) {
        const o = document.createElement("option");
        o.value = opt; o.textContent = opt;
        if (opt === f.default) o.selected = true;
        input.appendChild(o);
      }
    } else {
      input = document.createElement("input");
      input.type = f.type === "int" ? "number" : "text";
      input.value = f.default ?? "";
      if (f.type === "string[]") input.placeholder = "逗号分隔，如 SH600519,SZ000001";
    }
    input.dataset.field = f.name;
    input.dataset.fieldType = f.type;
    if (f.required) input.classList.add("required");
    form.appendChild(input);
  }

  document.getElementById("send-btn").disabled = false;
  document.getElementById("ep-stats").textContent = "";
  document.getElementById("ep-result").textContent = "";
  document.getElementById("show-all-btn").hidden = true;
  hideDownloads();
}

document.getElementById("send-btn").addEventListener("click", async () => {
  const ep = state.current;
  if (!ep) return;

  const body = {};
  document.querySelectorAll("#ep-form [data-field]").forEach(el => {
    const name = el.dataset.field;
    const t = el.dataset.fieldType;
    let v = el.value;
    if (t === "int") {
      const n = parseInt(v, 10);
      body[name] = isNaN(n) ? null : n;
    } else if (t === "string[]") {
      body[name] = v.split(",").map(s => s.trim()).filter(Boolean);
    } else {
      body[name] = v;
    }
  });

  const stats = document.getElementById("ep-stats");
  const result = document.getElementById("ep-result");
  const showAll = document.getElementById("show-all-btn");
  stats.textContent = "请求中...";
  stats.className = "";
  result.textContent = "";
  showAll.hidden = true;

  const t0 = performance.now();
  let resp;
  try {
    resp = await postJson(ep.path, body);
  } catch (e) {
    stats.className = "error";
    stats.textContent = `请求失败: ${e.message}`;
    return;
  }
  const t1 = performance.now();

  const lines = [
    `ok=${resp.ok}`,
    `serverElapsedMs=${resp.elapsedMs}`,
    `clientElapsedMs=${(t1 - t0).toFixed(0)}`,
  ];
  if (resp.rowCount != null) lines.push(`rowCount=${resp.rowCount}`);
  if (!resp.ok) {
    lines.push(`error=${resp.error}`);
    lines.push(`errorType=${resp.errorType}`);
    stats.className = "error";
  }

  // 多源 source 字段提示
  if (resp.data) {
    if (Array.isArray(resp.data) && resp.data.length > 0 && resp.data[0].source) {
      const sources = [...new Set(resp.data.map(r => r.source))];
      lines.push(`sources=${sources.join(",")}`);
    } else if (!Array.isArray(resp.data) && resp.data.source) {
      lines.push(`source=${resp.data.source}`);
    }
  }
  stats.textContent = lines.join("  |  ");

  const json = JSON.stringify(resp, null, 2);
  const allLines = json.split("\n");
  if (allLines.length > 200) {
    result.textContent = allLines.slice(0, 200).join("\n") + "\n... (still " + (allLines.length - 200) + " lines)";
    showAll.hidden = false;
    showAll.onclick = () => {
      result.textContent = json;
      showAll.hidden = true;
    };
  } else {
    result.textContent = json;
  }

  // v1.3.0 Sprint 3：巨潮公告端点成功返回时，渲染每行 PDF 下载链接。
  renderCninfoDownloads(ep, resp);
});

async function postJson(url, body) {
  const r = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body || {}),
  });
  return r.json();
}

// ── cninfo pdf download helpers ─────────────────────
function hideDownloads() {
  const box = document.getElementById("ep-downloads");
  const list = document.getElementById("ep-downloads-list");
  if (box) box.hidden = true;
  if (list) list.innerHTML = "";
}

function renderCninfoDownloads(ep, resp) {
  hideDownloads();
  if (!ep || ep.path !== "/api/cninfo/announcements") return;
  if (!resp || resp.ok !== true) return;
  const rows = resp.data;
  if (!Array.isArray(rows) || rows.length === 0) return;

  const box = document.getElementById("ep-downloads");
  const list = document.getElementById("ep-downloads-list");
  if (!box || !list) return;
  list.innerHTML = "";
  for (const row of rows) {
    // 字段可能是 camelCase（System.Text.Json 默认驼峰化 PascalCase 属性名）。
    const adjunctUrl = row.adjunctUrl || row.AdjunctUrl;
    const title = row.title || row.Title || "(无标题)";
    const date = row.publishDate || row.PublishDate || "";
    if (!adjunctUrl) continue;
    const li = document.createElement("li");
    const a = document.createElement("a");
    a.href = "/api/cninfo/pdf-download?adjunctUrl=" + encodeURIComponent(adjunctUrl);
    a.textContent = `⬇ ${date}  ${title}`;
    a.target = "_blank";
    a.rel = "noopener";
    li.appendChild(a);
    list.appendChild(li);
  }
  if (list.children.length > 0) box.hidden = false;
}

// ── tabs ────────────────────────────────────────────
function bindTabs() {
  document.querySelectorAll(".tab-btn").forEach(btn => {
    btn.addEventListener("click", () => {
      document.querySelectorAll(".tab-btn").forEach(b => b.classList.remove("active"));
      btn.classList.add("active");
      const target = btn.dataset.panel;
      document.querySelectorAll(".panel").forEach(p => {
        p.classList.toggle("hidden", p.id !== target);
      });
    });
  });
}

// ── load test panel ────────────────────────────────
function bindLoadTest() {
  const modeRadios = document.querySelectorAll('input[name="lt-mode"]');
  modeRadios.forEach(r => r.addEventListener("change", syncLoadModeInputs));
  document.getElementById("lt-target").addEventListener("change", onLoadTargetChange);
  document.getElementById("lt-run-btn").addEventListener("click", runLoadTest);
}

// 返回端点 protocol 徽章文本。
function protocolBadgeText(proto) {
  switch (proto) {
    case "tcp": return "[TCP]";
    case "http": return "[HTTP]";
    case "internal": return "[META]";
    default: return "[?]";
  }
}

function syncLoadModeInputs() {
  const mode = document.querySelector('input[name="lt-mode"]:checked').value;
  document.getElementById("lt-duration").disabled = mode !== "duration";
  document.getElementById("lt-total").disabled = mode !== "count";
}

async function loadLoadTargets() {
  let targets;
  try {
    targets = await fetch("/api/loadtest/list-targets").then(r => r.json());
  } catch (e) {
    document.getElementById("lt-status").textContent = "加载目标失败: " + e.message;
    return;
  }
  state.loadTargets = targets;
  const sel = document.getElementById("lt-target");
  sel.innerHTML = "";
  for (const t of targets) {
    const o = document.createElement("option");
    o.value = t.path;
    o.textContent = `[${t.group}] ${t.name}  ${t.path}`;
    sel.appendChild(o);
  }
  if (targets.length > 0) {
    sel.value = targets[0].path;
    onLoadTargetChange();
  }
}

function onLoadTargetChange() {
  const path = document.getElementById("lt-target").value;
  const t = state.loadTargets.find(x => x.path === path);
  if (!t) return;
  const body = t.defaultBody || {};
  document.getElementById("lt-body").value = JSON.stringify(body, null, 2);

  // 根据目标端点 protocol 调整表单限制
  const proto = t.protocol || "unknown";
  const concInput = document.getElementById("lt-concurrency");
  const concHint = document.getElementById("lt-concurrency-hint");
  const totalInput = document.getElementById("lt-total");
  const durInput = document.getElementById("lt-duration");
  const runBtn = document.getElementById("lt-run-btn");
  const targetBanner = document.getElementById("lt-target-banner");

  if (proto === "tcp") {
    concInput.max = 1;
    if (parseInt(concInput.value, 10) > 1) concInput.value = 1;
    concInput.title = "baostock TCP endpoint: concurrency locked to 1";
    concHint.hidden = false;
    concHint.textContent = "baostock TCP endpoint: concurrency locked to 1";
    totalInput.max = 200;
    totalInput.value = 50;
    durInput.max = 30;
    if (parseInt(durInput.value, 10) > 30) durInput.value = 30;
    runBtn.disabled = false;
    targetBanner.hidden = false;
    targetBanner.className = "target-banner banner-warn";
    targetBanner.textContent = "⚠️ TCP 端点：concurrency 锁定 1, total≤200, duration≤30s";
  } else if (proto === "http") {
    concInput.max = 100;
    concInput.title = "";
    concHint.hidden = true;
    concHint.textContent = "";
    totalInput.max = 100000;
    durInput.max = 300;
    runBtn.disabled = false;
    targetBanner.hidden = true;
    targetBanner.className = "target-banner";
    targetBanner.textContent = "";
  } else if (proto === "internal") {
    runBtn.disabled = true;
    concHint.hidden = false;
    concHint.textContent = "internal endpoint not available for load test";
    targetBanner.hidden = false;
    targetBanner.className = "target-banner banner-info";
    targetBanner.textContent = "internal endpoint not available for load test";
  } else {
    runBtn.disabled = false;
    concHint.hidden = true;
    targetBanner.hidden = true;
  }
}

async function runLoadTest() {
  if (state.loadRunning) return;

  const path = document.getElementById("lt-target").value;
  const target = state.loadTargets.find(x => x.path === path);
  const proto = target ? (target.protocol || "unknown") : "unknown";
  const bodyText = document.getElementById("lt-body").value.trim();
  let body;
  try {
    body = bodyText.length === 0 ? {} : JSON.parse(bodyText);
  } catch (e) {
    alert("Body JSON 解析失败: " + e.message);
    return;
  }
  const mode = document.querySelector('input[name="lt-mode"]:checked').value;
  const concurrency = parseInt(document.getElementById("lt-concurrency").value, 10) || 0;
  const warmup = parseInt(document.getElementById("lt-warmup").value, 10) || 0;
  const duration = parseInt(document.getElementById("lt-duration").value, 10) || 0;
  const total = parseInt(document.getElementById("lt-total").value, 10) || 0;

  // 前端拦截（仅体验提升）：baostock TCP 端点 concurrency>1 直接驳回。
  // 后端 Program.cs 会兰截，这里只是避免多走一趟。
  if (proto === "tcp" && concurrency > 1) {
    alert("baostock TCP 端点不支持 concurrency > 1（单条共享连接）。请设为 1 仅跑串行基线。");
    return;
  }

  const payload = {
    targetPath: path,
    targetBody: body,
    mode,
    concurrency,
    warmupRequests: warmup,
  };
  if (mode === "duration") payload.durationSeconds = duration;
  else payload.totalRequests = total;

  const btn = document.getElementById("lt-run-btn");
  const status = document.getElementById("lt-status");
  state.loadRunning = true;
  btn.disabled = true;
  btn.textContent = "运行中...";
  status.textContent = "";
  status.className = "";

  let resp;
  const t0 = performance.now();
  try {
    const r = await fetch("/api/loadtest/run", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    resp = await r.json();
    if (!r.ok && resp && resp.ok !== true) {
      status.className = "error";
      status.textContent = `HTTP ${r.status}: ${resp.error || "请求失败"}`;
    }
  } catch (e) {
    status.className = "error";
    status.textContent = "请求失败: " + e.message;
    state.loadRunning = false;
    btn.disabled = false;
    btn.textContent = "开始压测";
    return;
  }
  const t1 = performance.now();

  state.loadRunning = false;
  btn.disabled = false;
  btn.textContent = "开始压测";

  if (!resp || resp.ok !== true) {
    if (!status.textContent) {
      status.className = "error";
      status.textContent = (resp && resp.error) || "压测失败";
    }
    return;
  }

  status.textContent = `完成 (clientElapsed=${(t1 - t0).toFixed(0)}ms)`;
  renderLoadResult(resp);
}

function renderLoadResult(r) {
  document.getElementById("load-result-wrap").hidden = false;

  const cards = document.getElementById("lt-cards");
  cards.innerHTML = "";
  const cardData = [
    { label: "QPS", value: r.qps },
    { label: "错误率", value: (r.errorRate * 100).toFixed(2) + "%" },
    { label: "总请求", value: r.totalRequests },
    { label: "平均延迟 ms", value: r.latencyMs.mean },
    { label: "成功 / 错误", value: `${r.successCount} / ${r.errorCount}` },
    { label: "耗时 ms", value: r.elapsedMs },
  ];
  for (const c of cardData) {
    const el = document.createElement("div");
    el.className = "card";
    el.innerHTML = `<div class="card-label">${c.label}</div><div class="card-value">${c.value}</div>`;
    cards.appendChild(el);
  }

  const lat = r.latencyMs;
  const latRows = [
    ["min", lat.min], ["p50", lat.p50], ["p90", lat.p90],
    ["p95", lat.p95], ["p99", lat.p99], ["max", lat.max], ["mean", lat.mean],
  ];
  document.getElementById("lt-latency-table").innerHTML =
    "<tbody>" + latRows.map(([k, v]) => `<tr><th>${k}</th><td>${v}</td></tr>`).join("") + "</tbody>";

  // N-2 (v1.2.0-preview5)：startedAt/endedAt 本地化显示，UTC 原值保留在 title 里 hover 可见。
  // 后端返回的是 DateTime.UtcNow，序列化为 "2026-04-24T12:34:56.789Z"；直接喂 new Date() 即可。
  const timesEl = document.getElementById("lt-times");
  if (timesEl) {
    const fmt = (utc) => {
      if (!utc) return "—";
      const d = new Date(utc);
      return isNaN(d.getTime()) ? utc : d.toLocaleString("zh-CN") + " (本地)";
    };
    timesEl.innerHTML =
      `<span title="${r.startedAt || ""}">开始：${fmt(r.startedAt)}</span>` +
      ` &nbsp;·&nbsp; ` +
      `<span title="${r.endedAt || ""}">结束：${fmt(r.endedAt)}</span>`;
  }

  const errs = r.errorBreakdown || [];
  const errEl = document.getElementById("lt-errors-table");
  if (errs.length === 0) {
    errEl.innerHTML = "<tbody><tr><td>无</td></tr></tbody>";
  } else {
    errEl.innerHTML =
      "<thead><tr><th>errorType</th><th>count</th></tr></thead>" +
      "<tbody>" + errs.map(e => `<tr><td>${e.errorType}</td><td>${e.count}</td></tr>`).join("") + "</tbody>";
  }

  document.getElementById("lt-config").textContent = JSON.stringify(r.config, null, 2);
}
