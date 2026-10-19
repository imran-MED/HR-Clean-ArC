"use strict";

// Attendance upload page. Posts a machine export file to
// /api/attendance/upload, shows the normalized records, and stores the
// roster-reconciliation result for the dedicated Reconciliation page.

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

let lastRecords = [];

// ───────── Helpers ─────────
let toastTimer;
function toast(message) {
  const el = $("toast");
  el.textContent = message;
  el.hidden = false;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { el.hidden = true; }, 2800);
}

const pad2 = (n) => String(n).padStart(2, "0");

// Format an ISO timestamp back to "yyyy-MM-dd HH:mm:ss".
function formatTimestamp(iso) {
  const d = new Date(iso);
  if (isNaN(d.getTime())) return iso;
  return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())} `
       + `${pad2(d.getHours())}:${pad2(d.getMinutes())}:${pad2(d.getSeconds())}`;
}

// ───────── Upload ─────────
async function handleUpload() {
  const errorEl = $("upload-error");
  errorEl.hidden = true;

  const file = $("file").files[0];
  if (!file) {
    errorEl.textContent = "Please choose a file first.";
    errorEl.hidden = false;
    return;
  }

  const btn = $("upload-btn");
  btn.disabled = true;
  btn.querySelector(".btn-label").textContent = "Processing…";
  btn.querySelector(".spinner").hidden = false;

  try {
    const form = new FormData();
    form.append("file", file);

    const res = await fetch("/api/attendance/upload", {
      method: "POST",
      body: form,
    });
    const data = await res.json().catch(() => ({}));

    if (res.ok) {
      // Stash the reconciliation for the dedicated Reconciliation page.
      sessionStorage.setItem(RECON_KEY, JSON.stringify(data));
      renderResult(data);
      toast(`Processed ${data.importedCount} record(s) · `
          + `${data.totalMissingPeriods} missing · `
          + `${data.totalLateMinutes}m late · `
          + `${data.totalEarlyDepartureMinutes}m early`);
    } else {
      errorEl.textContent = data.error || `Upload failed (${res.status}).`;
      errorEl.hidden = false;
    }
  } catch {
    errorEl.textContent = "Could not reach the server.";
    errorEl.hidden = false;
  } finally {
    btn.disabled = false;
    btn.querySelector(".btn-label").textContent = "Upload & process";
    btn.querySelector(".spinner").hidden = true;
  }
}

// ───────── Rendering ─────────
function renderResult(data) {
  lastRecords = data.records || [];

  $("result-file").textContent = data.fileName || "";
  $("stat-total").textContent = data.totalLines;
  $("stat-imported").textContent = data.importedCount;
  $("stat-skipped").textContent = data.skippedCount;

  // The reconciliation itself lives on its own page.
  $("view-recon").textContent =
    `View roster reconciliation — ${data.totalMissingPeriods} missing, `
    + `${data.totalLateMinutes}m late →`;

  $("result-section").hidden = false;

  const tbody = $("result-rows");
  tbody.innerHTML = "";
  lastRecords.forEach((r) => {
    const tr = document.createElement("tr");
    tr.innerHTML =
      `<td>${r.lineNumber}</td>`
      + `<td class="id-old">${r.rawEmployeeId}</td>`
      + `<td class="id-badge">${r.employeeId}</td>`
      + `<td>${formatTimestamp(r.timestamp)}</td>`
      + `<td>${r.status}</td>`
      + `<td>${r.verify}</td>`
      + `<td>${r.workCode}</td>`
      + `<td>${r.reserved}</td>`;
    tbody.appendChild(tr);
  });

  const errors = data.errors || [];
  const errSection = $("errors-section");
  if (errors.length > 0) {
    const list = $("errors-list");
    list.innerHTML = "";
    errors.forEach((e) => {
      const li = document.createElement("li");
      li.textContent = e;
      list.appendChild(li);
    });
    errSection.hidden = false;
  } else {
    errSection.hidden = true;
  }
}

// ───────── Download normalized file ─────────
function handleDownload() {
  if (lastRecords.length === 0) {
    toast("Nothing to download.");
    return;
  }
  const lines = lastRecords.map((r) => [
    r.employeeId,                     // already 8 digits
    formatTimestamp(r.timestamp),
    r.status, r.verify, r.workCode, r.reserved,
  ].join("\t"));

  const blob = new Blob([lines.join("\r\n") + "\r\n"], { type: "text/plain" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = "attendance-normalized.txt";
  a.click();
  URL.revokeObjectURL(url);
}

// ───────── Wire up ─────────
document.addEventListener("DOMContentLoaded", () => {
  if (!session || !session.accessToken) return; // redirect already in flight

  $("topbar-username").textContent = session.username || session.userId || "";

  $("upload-btn").addEventListener("click", handleUpload);
  $("download-btn").addEventListener("click", handleDownload);
  $("logout-btn").addEventListener("click", () => {
    sessionStorage.removeItem(STORAGE_KEY);
    window.location.href = "/";
  });
});
