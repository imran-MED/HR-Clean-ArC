"use strict";

// Roster reconciliation page. Renders the result stored by the upload page
// (sessionStorage key "hrauth.reconciliation") with explicit Apply-driven
// filtering and per-row expandable detail (day-of-week + first/last punch).

const STORAGE_KEY = "hrauth.session";
const RECON_KEY = "hrauth.reconciliation";
const $ = (id) => document.getElementById(id);

// Guard: must be signed in.
let session = null;
try { session = JSON.parse(sessionStorage.getItem(STORAGE_KEY)); }
catch { session = null; }
if (!session || !session.accessToken) {
  window.location.replace("/");
}

// Two filter states: `pending` is what the inputs hold; `applied` is what the
// table actually filters by. Apply / Reset / stat-shortcuts commit pending →
// applied, then re-render.
const defaultFilters = () => ({
  search: "",
  from: "",
  to: "",
  flags: { late: true, early: true, missing: true, clean: true, noroster: true },
});

const state = {
  data: null,
  pending: defaultFilters(),
  applied: defaultFilters(),
  expanded: new Set(),
};

const DAY_NAMES = [
  "Sunday", "Monday", "Tuesday", "Wednesday",
  "Thursday", "Friday", "Saturday",
];

function escapeHtml(s) {
  return String(s).replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;").replaceAll(">", "&gt;");
}

// Parse a "yyyy-MM-dd" date string as a local Date (no timezone shift).
function parseDate(s) {
  if (!s) return null;
  const [y, m, d] = s.split("-").map(Number);
  if (!y || !m || !d) return null;
  return new Date(y, m - 1, d);
}

function dayName(dateStr) {
  const d = parseDate(dateStr);
  return d ? DAY_NAMES[d.getDay()] : "";
}

function longDateText(dateStr) {
  const d = parseDate(dateStr);
  if (!d) return dateStr;
  return d.toLocaleDateString(undefined, {
    weekday: "long", year: "numeric", month: "long", day: "numeric",
  });
}

const rowKey = (d) => `${d.employeeId}|${d.date}`;

// ───────── Classify / filter ─────────
function dayHasFlag(d, flag) {
  switch (flag) {
    case "late":     return d.hasRoster && d.lateMinutes > 0;
    case "early":    return d.hasRoster && d.earlyDepartureMinutes > 0;
    case "missing":  return d.hasRoster && d.missingCount > 0;
    case "noroster": return !d.hasRoster;
    case "clean":
      return d.hasRoster
        && d.missingCount === 0
        && d.lateMinutes === 0
        && d.earlyDepartureMinutes === 0;
    default: return false;
  }
}

function applyFilters(days) {
  const { search, from, to, flags } = state.applied;
  const q = search.trim().toLowerCase();
  const flagList = ["late", "early", "missing", "clean", "noroster"];
  const anyChecked = flagList.some((f) => flags[f]);

  return days.filter((d) => {
    if (q) {
      const hay = `${d.employeeId} ${d.employeeName || ""}`.toLowerCase();
      if (!hay.includes(q)) return false;
    }
    if (from && d.date < from) return false;
    if (to && d.date > to) return false;
    if (!anyChecked) return false;
    return flagList.some((f) => flags[f] && dayHasFlag(d, f));
  });
}

// ───────── Pending vs applied indicator ─────────
function filtersEqual(a, b) {
  if (a.search !== b.search || a.from !== b.from || a.to !== b.to) return false;
  for (const k of Object.keys(a.flags)) {
    if (a.flags[k] !== b.flags[k]) return false;
  }
  return true;
}

function refreshDirtyState() {
  const dirty = !filtersEqual(state.pending, state.applied);
  $("dirty-dot").hidden = !dirty;
  const apply = $("f-apply");
  apply.classList.toggle("is-dirty", dirty);
}

function commit(newFilters) {
  state.applied = JSON.parse(JSON.stringify(newFilters));
  state.pending = JSON.parse(JSON.stringify(newFilters));
  syncInputsFromPending();
  refreshDirtyState();
  render();
}

// ───────── Render ─────────
function render() {
  const data = state.data;
  if (!data) return;
  const days = data.days || [];

  $("result-file").textContent = data.fileName || "";
  $("rec-days").textContent = days.length;
  $("rec-missing").textContent = data.totalMissingPeriods;
  $("rec-late").textContent = data.totalLateMinutes;
  $("rec-early").textContent = data.totalEarlyDepartureMinutes;
  $("rec-noroster").textContent = data.employeesWithoutRoster;
  $("rec-allgood").hidden = data.totalMissingPeriods > 0
    || data.totalLateMinutes > 0
    || data.totalEarlyDepartureMinutes > 0;

  const filtered = applyFilters(days);
  $("filter-status").textContent =
    `Showing ${filtered.length} of ${days.length} employee-days`;

  // Highlight which stat-card is "soloed" by the current applied filters.
  refreshActiveStat();

  const tbody = $("rec-rows");
  tbody.innerHTML = "";

  if (filtered.length === 0) {
    const tr = document.createElement("tr");
    tr.innerHTML = `<td colspan="8" class="empty-row">
      No rows match the current filters.</td>`;
    tbody.appendChild(tr);
    return;
  }

  filtered.forEach((d) => {
    tbody.appendChild(buildRow(d));
    if (state.expanded.has(rowKey(d))) {
      tbody.appendChild(buildDetailRow(d));
    }
  });
}

function refreshActiveStat() {
  const f = state.applied.flags;
  const flagList = ["late", "early", "missing", "clean", "noroster"];
  const checked = flagList.filter((x) => f[x]);
  // Solo iff exactly one flag is on AND no other filters are set.
  const isFresh =
    state.applied.search === "" && !state.applied.from && !state.applied.to;
  const solo = isFresh && checked.length === 1 ? checked[0] : null;
  const isAll = isFresh && checked.length === flagList.length;

  document.querySelectorAll(".stats-clickable .stat").forEach((b) => {
    const s = b.dataset.stat;
    const active = (s === "all" && isAll) || (s === solo);
    b.classList.toggle("active", active);
  });
}

function buildRow(d) {
  let periodsHtml;
  if (!d.hasRoster) {
    periodsHtml = `<span class="muted">No roster found</span>`;
  } else {
    periodsHtml = d.periods.map((p) => {
      const label = `${escapeHtml(p.name)} ${p.startTime}–${p.endTime}`;
      if (p.isMissing) {
        return `<span class="pill pill-bad">${label} ✗ missing</span>`;
      }
      const flags = [];
      if (p.isLate) flags.push(`late ${p.lateMinutes}m`);
      if (p.isEarlyDeparture) flags.push(`early ${p.earlyDepartureMinutes}m`);
      if (flags.length === 0) {
        return `<span class="pill pill-ok">${label} ✓</span>`;
      }
      return `<span class="pill pill-warn">${label} · ${flags.join(", ")}</span>`;
    }).join("");
  }

  const key = rowKey(d);
  const isOpen = state.expanded.has(key);
  const tr = document.createElement("tr");
  tr.className = "clickable-row" + (isOpen ? " row-open" : "");
  tr.dataset.key = key;
  tr.innerHTML =
    `<td class="chev-cell">${isOpen ? "▾" : "▸"}</td>`
    + `<td class="id-badge">${escapeHtml(d.employeeId)}</td>`
    + `<td dir="auto">${escapeHtml(d.employeeName || "—")}</td>`
    + `<td>${escapeHtml(d.date)}</td>`
    + `<td class="muted">${escapeHtml(dayName(d.date))}</td>`
    + `<td>${escapeHtml(d.shiftName || "—")}</td>`
    + `<td class="periods-cell">${periodsHtml}</td>`
    + `<td>${d.punchCount}</td>`;
  tr.addEventListener("click", () => toggleExpand(key));
  return tr;
}

function buildDetailRow(d) {
  const tr = document.createElement("tr");
  tr.className = "detail-row";
  tr.innerHTML = `<td colspan="8">${buildDetailHtml(d)}</td>`;
  return tr;
}

function buildDetailHtml(d) {
  const header = `
    <div class="detail-head">
      <div>
        <div class="detail-title">${escapeHtml(longDateText(d.date))}</div>
        <div class="muted small">
          ${escapeHtml(d.employeeId)} · ${escapeHtml(d.employeeName || "—")}
          · Shift: ${escapeHtml(d.shiftName || "—")}
          · ${d.punchCount} punch${d.punchCount === 1 ? "" : "es"}
        </div>
      </div>
    </div>`;

  if (!d.hasRoster) {
    return header + `<p class="muted" style="margin-top:10px;">
      No roster scheduled for this date — punches were not checked against
      a schedule.</p>`;
  }
  if (d.periods.length === 0) {
    return header + `<p class="muted" style="margin-top:10px;">
      Roster has no periods on this date.</p>`;
  }

  const rows = d.periods.map((p) => {
    let status, statusClass;
    if (p.isMissing) {
      status = "Missing — no punch within ±15 min";
      statusClass = "pill-bad";
    } else if (p.isLate && p.isEarlyDeparture) {
      status = `Late ${p.lateMinutes}m + Early-out ${p.earlyDepartureMinutes}m`;
      statusClass = "pill-warn";
    } else if (p.isLate) {
      status = `Late ${p.lateMinutes}m`;
      statusClass = "pill-warn";
    } else if (p.isEarlyDeparture) {
      status = `Early-out ${p.earlyDepartureMinutes}m`;
      statusClass = "pill-warn";
    } else {
      status = "On time";
      statusClass = "pill-ok";
    }

    return `<tr>
      <td>${escapeHtml(p.name)}</td>
      <td>${p.startTime}–${p.endTime}</td>
      <td>${p.firstPunch || "—"}</td>
      <td>${p.lastPunch || "—"}</td>
      <td>${p.punchCount}</td>
      <td><span class="pill ${statusClass}">${status}</span></td>
    </tr>`;
  }).join("");

  return header + `
    <div class="table-wrap" style="margin-top:10px;">
      <table class="data data-inner">
        <thead><tr>
          <th>Period</th><th>Scheduled</th>
          <th>First punch</th><th>Last punch</th>
          <th>Punches</th><th>Status</th>
        </tr></thead>
        <tbody>${rows}</tbody>
      </table>
    </div>`;
}

function toggleExpand(key) {
  if (state.expanded.has(key)) state.expanded.delete(key);
  else state.expanded.add(key);
  render();
}

// ───────── Stat-card filter shortcuts (commit immediately) ─────────
function applyStatShortcut(stat) {
  const next = defaultFilters();
  if (stat !== "all") {
    Object.keys(next.flags).forEach((k) => { next.flags[k] = false; });
    if (stat in next.flags) next.flags[stat] = true;
  }
  commit(next);
}

// ───────── Pending state input handlers ─────────
function syncInputsFromPending() {
  $("f-search").value = state.pending.search;
  $("f-from").value = state.pending.from;
  $("f-to").value = state.pending.to;
  document.querySelectorAll(".chip").forEach((label) => {
    const cb = label.querySelector("input");
    const flag = cb.dataset.flag;
    cb.checked = !!state.pending.flags[flag];
    label.classList.toggle("checked", cb.checked);
  });
}

// ───────── Wire up ─────────
document.addEventListener("DOMContentLoaded", () => {
  if (!session || !session.accessToken) return;

  $("topbar-username").textContent = session.username || session.userId || "";
  $("logout-btn").addEventListener("click", () => {
    sessionStorage.removeItem(STORAGE_KEY);
    window.location.href = "/";
  });

  let data = null;
  try { data = JSON.parse(sessionStorage.getItem(RECON_KEY)); }
  catch { data = null; }

  if (!data || !Array.isArray(data.days)) {
    $("empty-section").hidden = false;
    return;
  }

  state.data = data;
  $("recon-section").hidden = false;
  syncInputsFromPending();

  // Pending writes (do NOT trigger a render until Apply).
  $("f-search").addEventListener("input", (e) => {
    state.pending.search = e.target.value;
    refreshDirtyState();
  });
  $("f-search").addEventListener("keydown", (e) => {
    if (e.key === "Enter") commit(state.pending);
  });
  $("f-from").addEventListener("change", (e) => {
    state.pending.from = e.target.value;
    refreshDirtyState();
  });
  $("f-to").addEventListener("change", (e) => {
    state.pending.to = e.target.value;
    refreshDirtyState();
  });
  document.querySelectorAll(".chip input").forEach((cb) => {
    cb.addEventListener("change", () => {
      const label = cb.closest(".chip");
      state.pending.flags[cb.dataset.flag] = cb.checked;
      label.classList.toggle("checked", cb.checked);
      refreshDirtyState();
    });
  });

  $("f-apply").addEventListener("click", () => commit(state.pending));
  $("f-reset").addEventListener("click", () => {
    state.expanded.clear();
    commit(defaultFilters());
  });

  // Clickable stat cards — these commit instantly.
  document.querySelectorAll(".stats-clickable .stat").forEach((b) => {
    b.addEventListener("click", () => applyStatShortcut(b.dataset.stat));
  });

  refreshDirtyState();
  render();
});
