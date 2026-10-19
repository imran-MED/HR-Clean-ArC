"use strict";

// Employees page — two tabs:
//   • Directory  → GET /api/employees + POST /api/employees
//   • Import CSV → POST /api/employees/import-csv

const STORAGE_KEY = "hrauth.session";
const $ = (id) => document.getElementById(id);

let session = null;
try { session = JSON.parse(sessionStorage.getItem(STORAGE_KEY)); }
catch { session = null; }
if (!session || !session.accessToken) {
  window.location.replace("/");
}

let toastTimer;
function toast(msg) {
  const el = $("toast");
  el.textContent = msg;
  el.hidden = false;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { el.hidden = true; }, 2400);
}

function escapeHtml(s) {
  return String(s).replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;").replaceAll(">", "&gt;");
}

// ───────── Tabs ─────────
function showTab(which) {
  $("tab-directory").classList.toggle("active", which === "directory");
  $("tab-import").classList.toggle("active", which === "import");
  $("panel-directory").hidden = which !== "directory";
  $("panel-import").hidden = which !== "import";
}

// ───────── Directory tab ─────────
const dir = {
  search: "",
  page: 1,
  pageSize: 25,
  total: 0,
};

let searchDebounce;

async function loadDirectory() {
  const params = new URLSearchParams({
    page: String(dir.page),
    pageSize: String(dir.pageSize),
  });
  if (dir.search) params.set("search", dir.search);

  $("dir-status").textContent = "Loading…";
  $("dir-error").hidden = true;
  try {
    const res = await fetch(`/api/employees?${params}`);
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const data = await res.json();
    dir.total = data.total;
    renderDirectory(data.items);
    $("hero-total").textContent = data.total;

    const start = (data.page - 1) * data.pageSize + 1;
    const end = Math.min(start + data.items.length - 1, data.total);
    $("dir-status").textContent =
      data.total === 0
        ? "No matches."
        : `Showing ${start}–${end} of ${data.total}`;
    $("page-label").textContent = `Page ${data.page}`;
    $("page-prev").disabled = data.page <= 1;
    $("page-next").disabled = end >= data.total;
  } catch (e) {
    $("dir-error").textContent = `Failed to load: ${e.message}`;
    $("dir-error").hidden = false;
    $("dir-status").textContent = "";
  }
}

function renderDirectory(items) {
  const tbody = $("dir-rows");
  tbody.innerHTML = "";
  if (items.length === 0) return;
  items.forEach((e) => {
    const tr = document.createElement("tr");
    const roles = (e.roles || []).length
      ? e.roles.map((r) =>
          `<span class="pill pill-role">${escapeHtml(r)}</span>`).join(" ")
      : `<span class="muted">—</span>`;
    // Supervisor resolves to a name; show "name (id)" when both are known,
    // fall back to whichever is present.
    let supervisor = `<span class="muted">—</span>`;
    if (e.supervisorName) {
      supervisor = `<span dir="auto">${escapeHtml(e.supervisorName)}</span>`
        + ` <span class="muted small">(${escapeHtml(e.supervisorEmployeeId || "")})</span>`;
    } else if (e.supervisorEmployeeId) {
      supervisor = `<span class="id-badge">${escapeHtml(e.supervisorEmployeeId)}</span>`;
    }
    tr.innerHTML =
      `<td class="id-badge">${escapeHtml(e.employeeId)}</td>`
      + `<td dir="auto">${escapeHtml(e.displayName || "—")}</td>`
      + `<td>${escapeHtml(e.userName || "—")}</td>`
      + `<td dir="auto">${escapeHtml(e.position || "—")}</td>`
      + `<td>${supervisor}</td>`
      + `<td>${roles}</td>`;
    tbody.appendChild(tr);
  });
}

async function handleAddEmployee(event) {
  event.preventDefault();
  const errEl = $("add-error");
  errEl.hidden = true;

  const employeeId = $("add-id").value.trim();
  const displayName = $("add-name").value.trim();
  const userName = $("add-username").value.trim();
  const position = $("add-position").value.trim();
  const supervisorEmployeeId = $("add-supervisor").value.trim();

  if (!employeeId || !displayName) {
    errEl.textContent = "Employee ID and name are required.";
    errEl.hidden = false;
    return;
  }

  const btn = $("add-btn");
  btn.disabled = true;
  btn.querySelector(".btn-label").textContent = "Adding…";
  btn.querySelector(".spinner").hidden = false;

  try {
    const res = await fetch("/api/employees", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        employeeId,
        displayName,
        userName: userName || null,
        position: position || null,
        supervisorEmployeeId: supervisorEmployeeId || null,
      }),
    });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.error || `HTTP ${res.status}`);

    toast(`Added ${data.employeeId} — ${data.displayName}`);
    $("add-id").value = "";
    $("add-name").value = "";
    $("add-username").value = "";
    $("add-position").value = "";
    $("add-supervisor").value = "";

    // Re-search for the new ID so the user immediately sees their addition.
    $("emp-search").value = data.employeeId;
    dir.search = data.employeeId;
    dir.page = 1;
    loadDirectory();
  } catch (e) {
    errEl.textContent = e.message;
    errEl.hidden = false;
  } finally {
    btn.disabled = false;
    btn.querySelector(".btn-label").textContent = "Add employee";
    btn.querySelector(".spinner").hidden = true;
  }
}

// ───────── Import CSV tab ─────────
async function handleImport(event) {
  event.preventDefault();

  const errEl = $("import-error");
  errEl.hidden = true;

  const file = $("file-input").files[0];
  if (!file) {
    errEl.textContent = "Please choose a CSV file first.";
    errEl.hidden = false;
    return;
  }

  const btn = $("import-btn");
  btn.disabled = true;
  btn.querySelector(".btn-label").textContent = "Importing…";
  btn.querySelector(".spinner").hidden = false;

  try {
    const form = new FormData();
    form.append("file", file);
    const res = await fetch("/api/employees/import-csv", {
      method: "POST", body: form,
    });
    const data = await res.json().catch(() => ({}));
    if (res.ok) {
      renderImportResult(data);
      // Refresh the hero counter / directory in the background.
      loadDirectory();
    } else {
      errEl.textContent = data.error || `Import failed (${res.status}).`;
      errEl.hidden = false;
    }
  } catch {
    errEl.textContent = "Could not reach the server.";
    errEl.hidden = false;
  } finally {
    btn.disabled = false;
    btn.querySelector(".btn-label").textContent = "Import";
    btn.querySelector(".spinner").hidden = true;
  }
}

// Import the directory straight from the HR API (no file). Uses the signed-in
// user's token; the HR API enforces directory-read authorization.
async function handleHrApiImport() {
  const errEl = $("import-error");
  errEl.hidden = true;

  const btn = $("hrapi-import-btn");
  btn.disabled = true;
  btn.querySelector(".btn-label").textContent = "Importing…";
  btn.querySelector(".spinner").hidden = false;

  try {
    const res = await fetch("/api/employees/import-from-hrapi", {
      method: "POST",
      headers: { "Authorization": `Bearer ${session.accessToken}` },
    });
    const data = await res.json().catch(() => ({}));
    if (res.ok) {
      renderImportResult(data);
      loadDirectory();
    } else {
      errEl.textContent = data.error || `Import failed (${res.status}).`;
      errEl.hidden = false;
    }
  } catch {
    errEl.textContent = "Could not reach the server.";
    errEl.hidden = false;
  } finally {
    btn.disabled = false;
    btn.querySelector(".btn-label").textContent = "Import from HR API";
    btn.querySelector(".spinner").hidden = true;
  }
}

function renderImportResult(data) {
  $("result-file").textContent = data.fileName || "";
  $("stat-parsed").textContent = data.linesParsed;
  $("stat-imported").textContent = data.rowsImported;
  $("stat-skipped").textContent = data.rowsSkipped;
  $("stat-total").textContent = data.totalEmployeesInDb;

  const warnings = data.warnings || [];
  const wrap = $("warnings-wrap");
  const list = $("warnings-list");
  list.innerHTML = "";
  if (warnings.length > 0) {
    warnings.forEach((w) => {
      const li = document.createElement("li");
      li.textContent = w;
      list.appendChild(li);
    });
    wrap.hidden = false;
  } else {
    wrap.hidden = true;
  }

  $("result-section").hidden = false;
}

// ───────── Wire up ─────────
document.addEventListener("DOMContentLoaded", () => {
  if (!session || !session.accessToken) return;

  $("topbar-username").textContent = session.username || session.userId || "";
  $("logout-btn").addEventListener("click", () => {
    sessionStorage.removeItem(STORAGE_KEY);
    window.location.href = "/";
  });

  $("tab-directory").addEventListener("click", () => showTab("directory"));
  $("tab-import").addEventListener("click", () => showTab("import"));

  $("add-form").addEventListener("submit", handleAddEmployee);
  $("import-form").addEventListener("submit", handleImport);
  $("hrapi-import-btn").addEventListener("click", handleHrApiImport);

  $("emp-search").addEventListener("input", (e) => {
    dir.search = e.target.value;
    dir.page = 1;
    clearTimeout(searchDebounce);
    searchDebounce = setTimeout(loadDirectory, 200);
  });
  $("emp-pagesize").addEventListener("change", (e) => {
    dir.pageSize = Number(e.target.value) || 25;
    dir.page = 1;
    loadDirectory();
  });
  $("page-prev").addEventListener("click", () => {
    if (dir.page > 1) { dir.page--; loadDirectory(); }
  });
  $("page-next").addEventListener("click", () => {
    dir.page++; loadDirectory();
  });

  loadDirectory();
});
