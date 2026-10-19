"use strict";

// Employee roster page. Fetches /api/roster/{employeeId}?date=yyyy-MM-dd
// and renders the day's shift periods.

const STORAGE_KEY = "hrauth.session";
const $ = (id) => document.getElementById(id);

// Guard: must be signed in.
let session = null;
try { session = JSON.parse(sessionStorage.getItem(STORAGE_KEY)); }
catch { session = null; }
if (!session || !session.accessToken) {
  window.location.replace("/");
}

// ───────── Fetch + render ─────────
async function fetchRoster() {
  const errorEl = $("roster-error");
  errorEl.hidden = true;
  $("roster-section").hidden = true;
  $("empty-section").hidden = true;

  const employeeId = $("employeeId").value.trim();
  if (!employeeId) {
    errorEl.textContent = "Enter an employee ID.";
    errorEl.hidden = false;
    return;
  }
  const date = $("date").value; // yyyy-MM-dd or ""

  const btn = $("fetch-btn");
  btn.disabled = true;
  btn.querySelector(".btn-label").textContent = "Loading…";
  btn.querySelector(".spinner").hidden = false;

  try {
    const url = `/api/roster/${encodeURIComponent(employeeId)}`
              + (date ? `?date=${encodeURIComponent(date)}` : "");
    const res = await fetch(url);
    const data = await res.json().catch(() => ({}));

    if (res.ok) {
      render(data);
    } else {
      errorEl.textContent = data.error || `Lookup failed (${res.status}).`;
      errorEl.hidden = false;
    }
  } catch {
    errorEl.textContent = "Could not reach the server.";
    errorEl.hidden = false;
  } finally {
    btn.disabled = false;
    btn.querySelector(".btn-label").textContent = "Get roster";
    btn.querySelector(".spinner").hidden = true;
  }
}

function render(data) {
  if (!data.hasRoster) {
    $("empty-text").textContent =
      `No shift assigned for ${data.employeeId} on ${data.date}.`;
    $("empty-section").hidden = false;
    return;
  }

  $("shift-name").textContent = data.shiftName || "Shift";
  $("shift-range").textContent = `${data.shiftStart} → ${data.shiftEnd}`;
  $("shift-meta").textContent =
    `${data.employeeId} · ${data.date} · ${data.periods.length} period(s)`;

  const tbody = $("period-rows");
  tbody.innerHTML = "";
  data.periods.forEach((p, i) => {
    const tr = document.createElement("tr");
    tr.innerHTML =
      `<td>${i + 1}</td>`
      + `<td>${p.name}</td>`
      + `<td>${p.startTime}</td>`
      + `<td>${p.endTime}</td>`
      + `<td>${p.requiredHours} h</td>`;
    tbody.appendChild(tr);
  });
  $("roster-section").hidden = false;
}

// ───────── Wire up ─────────
document.addEventListener("DOMContentLoaded", () => {
  if (!session || !session.accessToken) return; // redirect already in flight

  $("topbar-username").textContent = session.username || session.userId || "";

  // Prefill with the signed-in employee and today's date.
  $("employeeId").value = session.userId || "";
  const now = new Date();
  $("date").value = `${now.getFullYear()}-`
    + `${String(now.getMonth() + 1).padStart(2, "0")}-`
    + `${String(now.getDate()).padStart(2, "0")}`;

  $("fetch-btn").addEventListener("click", fetchRoster);
  $("logout-btn").addEventListener("click", () => {
    sessionStorage.removeItem(STORAGE_KEY);
    window.location.href = "/";
  });

  // Auto-load the signed-in employee's roster on open.
  if (session.userId) fetchRoster();
});
