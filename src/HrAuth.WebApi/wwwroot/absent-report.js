"use strict";

// Daily absent report page. Loads /api/reports/daily-absent for a date range
// and lets the operator record an attendance adjustment for a row (proxied to
// the HR API, which enforces the signed-in user's rights).

const STORAGE_KEY = "hrauth.session";
const $ = (id) => document.getElementById(id);

// Guard: must be signed in.
let session = null;
try { session = JSON.parse(sessionStorage.getItem(STORAGE_KEY)); }
catch { session = null; }
if (!session || !session.accessToken) {
  window.location.replace("/");
}

function toast(message, isError = false) {
  const el = $("toast");
  el.textContent = message;
  el.classList.toggle("toast-error", isError);
  el.hidden = false;
  clearTimeout(toast._t);
  toast._t = setTimeout(() => { el.hidden = true; }, 4000);
}

function groupLabel(key) {
  if (key === "GRP_MORNING") return "Morning";
  if (key === "GRP_EVENING") return "Evening";
  return key; // P_7, P_8, …
}

// ───────── Load + render ─────────
async function loadReport() {
  const errorEl = $("report-error");
  errorEl.hidden = true;
  $("report-section").hidden = true;
  $("empty-section").hidden = true;

  const from = $("from").value;
  const to = $("to").value;
  if (!from || !to) {
    errorEl.textContent = "Choose both dates.";
    errorEl.hidden = false;
    return;
  }

  const btn = $("load-btn");
  btn.disabled = true;
  btn.querySelector(".btn-label").textContent = "Loading…";
  btn.querySelector(".spinner").hidden = false;

  try {
    const employeeId = $("employeeId").value.trim();
    const url = `/api/reports/daily-absent?from=${encodeURIComponent(from)}`
              + `&to=${encodeURIComponent(to)}`
              + (employeeId ? `&employeeId=${encodeURIComponent(employeeId)}` : "");
    const res = await fetch(url, {
      headers: { "Authorization": `Bearer ${session.accessToken}` },
    });
    const data = await res.json().catch(() => ({}));

    if (res.ok) {
      render(Array.isArray(data) ? data : []);
    } else {
      errorEl.textContent = data.error || `Report failed (${res.status}).`;
      errorEl.hidden = false;
    }
  } catch {
    errorEl.textContent = "Could not reach the server.";
    errorEl.hidden = false;
  } finally {
    btn.disabled = false;
    btn.querySelector(".btn-label").textContent = "Load report";
    btn.querySelector(".spinner").hidden = true;
  }
}

function render(rows) {
  if (!rows.length) {
    $("empty-section").hidden = false;
    return;
  }

  const employees = new Set(rows.map((r) => r.employeeId));
  $("report-meta").textContent =
    `${rows.length} row(s) · ${employees.size} employee(s)`;

  const tbody = $("report-rows");
  tbody.innerHTML = "";
  rows.forEach((r) => {
    const tr = document.createElement("tr");

    const cells = [
      r.attendDate,
      r.employeeId,
      r.employeeName || "—",
      r.position || "—",
      r.department || "—",
      groupLabel(r.groupKey),
      r.periods || "—",
      r.requiredHours,
      r.presentHours,
      r.leaveHours,
      r.attendanceStatus || "—",
    ];
    cells.forEach((value, i) => {
      const td = document.createElement("td");
      if (i === 5) {
        const pill = document.createElement("span");
        pill.className = "pill pill-role";
        pill.textContent = value;
        td.appendChild(pill);
      } else {
        td.textContent = value;
      }
      tr.appendChild(td);
    });

    const actionTd = document.createElement("td");
    const adjustBtn = document.createElement("button");
    adjustBtn.className = "btn btn-ghost btn-sm";
    adjustBtn.textContent = "Adjust";
    adjustBtn.addEventListener("click", () => adjust(r, adjustBtn));
    actionTd.appendChild(adjustBtn);
    tr.appendChild(actionTd);

    tbody.appendChild(tr);
  });
  $("report-section").hidden = false;
}

// ───────── Attendance adjustment ─────────
async function adjust(row, btn) {
  const label = `${row.employeeName || row.employeeId} — ${row.attendDate}`
              + ` (${groupLabel(row.groupKey)})`;
  const notes = prompt(`Record attendance adjustment for:\n${label}\n\nNotes (optional):`, "");
  if (notes === null) return; // cancelled

  btn.disabled = true;
  btn.textContent = "Saving…";
  try {
    const res = await fetch("/api/reports/daily-absent/adjust", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${session.accessToken}`,
      },
      body: JSON.stringify({
        employeeId: row.employeeId,
        attendanceDate: row.attendDate,
        notes: notes || null,
      }),
    });
    const data = await res.json().catch(() => ({}));

    if (res.ok) {
      toast(`Adjustment recorded for ${row.employeeId} on ${row.attendDate}.`);
      btn.textContent = "Adjusted ✓";
    } else {
      toast(data.error || `Adjustment failed (${res.status}).`, true);
      btn.disabled = false;
      btn.textContent = "Adjust";
    }
  } catch {
    toast("Could not reach the server.", true);
    btn.disabled = false;
    btn.textContent = "Adjust";
  }
}

// ───────── Wire up ─────────
document.addEventListener("DOMContentLoaded", () => {
  if (!session || !session.accessToken) return; // redirect already in flight

  $("topbar-username").textContent = session.username || session.userId || "";

  const now = new Date();
  const today = `${now.getFullYear()}-`
    + `${String(now.getMonth() + 1).padStart(2, "0")}-`
    + `${String(now.getDate()).padStart(2, "0")}`;
  $("from").value = today;
  $("to").value = today;

  $("load-btn").addEventListener("click", loadReport);
  $("logout-btn").addEventListener("click", () => {
    sessionStorage.removeItem(STORAGE_KEY);
    window.location.href = "/";
  });

  loadReport(); // auto-load today's absences
});
