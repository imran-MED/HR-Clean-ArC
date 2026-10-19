"use strict";

// Roles page — two tabs:
//   • Catalog   → /api/roles  (GET, POST, PUT, DELETE)
//   • Assignment → /api/employees  (GET list) + /api/employees/:id/roles (PUT)

const STORAGE_KEY = "hrauth.session";
const $ = (id) => document.getElementById(id);

// Auth guard.
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

// ───────── Tab switching ─────────
function showTab(which) {
  $("tab-catalog").classList.toggle("active", which === "catalog");
  $("tab-assignment").classList.toggle("active", which === "assignment");
  $("panel-catalog").hidden = which !== "catalog";
  $("panel-assignment").hidden = which !== "assignment";
  if (which === "assignment" && !assignState.loaded) loadEmployees();
}

// ───────── Catalog ─────────
let catalog = [];

async function loadCatalog() {
  try {
    const res = await fetch("/api/roles");
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    catalog = await res.json();
    renderCatalog();
  } catch (e) {
    $("catalog-rows").innerHTML =
      `<tr><td colspan="4" class="error">Failed to load: ${e.message}</td></tr>`;
  }
}

function renderCatalog() {
  const tbody = $("catalog-rows");
  if (catalog.length === 0) {
    tbody.innerHTML =
      `<tr><td colspan="4" class="muted">No roles yet — add one above.</td></tr>`;
    return;
  }
  tbody.innerHTML = "";
  catalog.forEach((r) => {
    const tr = document.createElement("tr");
    tr.dataset.name = r.name;
    tr.innerHTML =
      `<td><span class="pill pill-role">${escapeHtml(r.name)}</span></td>`
      + `<td><input class="desc-input" type="text" maxlength="256" `
      + `        value="${escapeAttr(r.description || "")}" /></td>`
      + `<td>${r.assignedTo}</td>`
      + `<td class="row-actions">`
      + `  <button class="btn btn-sm save-desc">Save</button>`
      + `  <button class="btn btn-sm btn-danger delete-role">Delete</button>`
      + `</td>`;
    tbody.appendChild(tr);
  });
}

async function handleAddRole(event) {
  event.preventDefault();
  const errorEl = $("catalog-error");
  errorEl.hidden = true;
  const name = $("add-name").value.trim();
  const description = $("add-desc").value.trim();
  if (!name) {
    errorEl.textContent = "Role name is required.";
    errorEl.hidden = false;
    return;
  }
  try {
    const res = await fetch("/api/roles", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name, description: description || null }),
    });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.error || `HTTP ${res.status}`);
    $("add-name").value = "";
    $("add-desc").value = "";
    await loadCatalog();
    toast(`Added role "${data.name}".`);
  } catch (e) {
    errorEl.textContent = e.message;
    errorEl.hidden = false;
  }
}

async function handleCatalogRowClick(event) {
  const tr = event.target.closest("tr[data-name]");
  if (!tr) return;
  const name = tr.dataset.name;

  if (event.target.classList.contains("save-desc")) {
    const description = tr.querySelector(".desc-input").value.trim();
    try {
      const res = await fetch(`/api/roles/${encodeURIComponent(name)}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name, description: description || null }),
      });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(data.error || `HTTP ${res.status}`);
      toast(`Saved "${name}".`);
    } catch (e) {
      toast(`Save failed: ${e.message}`);
    }
  } else if (event.target.classList.contains("delete-role")) {
    if (!confirm(
      `Delete role "${name}"? It will also be removed from any employees who had it.`)) return;
    try {
      const res = await fetch(`/api/roles/${encodeURIComponent(name)}`, {
        method: "DELETE",
      });
      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error(data.error || `HTTP ${res.status}`);
      }
      await loadCatalog();
      toast(`Deleted "${name}".`);
    } catch (e) {
      toast(`Delete failed: ${e.message}`);
    }
  }
}

// ───────── Assignment ─────────
const assignState = {
  loaded: false,
  search: "",
  page: 1,
  pageSize: 25,
  total: 0,
};

let searchDebounce;

async function loadEmployees() {
  const params = new URLSearchParams({
    page: String(assignState.page),
    pageSize: String(assignState.pageSize),
  });
  if (assignState.search) params.set("search", assignState.search);

  $("assign-status").textContent = "Loading…";
  $("assign-error").hidden = true;
  try {
    const res = await fetch(`/api/employees?${params}`);
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const data = await res.json();
    assignState.total = data.total;
    assignState.loaded = true;

    renderAssignment(data.items);
    const start = (data.page - 1) * data.pageSize + 1;
    const end = Math.min(start + data.items.length - 1, data.total);
    $("assign-status").textContent =
      data.total === 0
        ? "No matches."
        : `Showing ${start}–${end} of ${data.total}`;
    $("page-label").textContent = `Page ${data.page}`;
    $("page-prev").disabled = data.page <= 1;
    $("page-next").disabled = end >= data.total;
  } catch (e) {
    $("assign-error").textContent = `Failed to load: ${e.message}`;
    $("assign-error").hidden = false;
    $("assign-status").textContent = "";
  }
}

function renderAssignment(items) {
  const tbody = $("assign-rows");
  tbody.innerHTML = "";

  if (items.length === 0) return;
  if (catalog.length === 0) {
    tbody.innerHTML =
      `<tr><td colspan="5" class="muted">`
      + `Define at least one role in the Catalog tab first.`
      + `</td></tr>`;
    return;
  }

  items.forEach((e) => {
    const tr = document.createElement("tr");
    tr.dataset.id = e.employeeId;
    const checks = catalog.map((r) => {
      const checked = e.roles.includes(r.name) ? "checked" : "";
      return `<label class="role-check"><input type="checkbox" `
        + `value="${escapeAttr(r.name)}" ${checked} />`
        + `${escapeHtml(r.name)}</label>`;
    }).join(" ");

    tr.innerHTML =
      `<td class="id-badge">${escapeHtml(e.employeeId)}</td>`
      + `<td dir="auto">${escapeHtml(e.displayName || "—")}</td>`
      + `<td>${escapeHtml(e.userName || "—")}</td>`
      + `<td class="role-checks">${checks}</td>`
      + `<td><button class="btn btn-sm save-roles">Save</button></td>`;
    tbody.appendChild(tr);
  });
}

async function handleAssignmentClick(event) {
  if (!event.target.classList.contains("save-roles")) return;
  const tr = event.target.closest("tr[data-id]");
  if (!tr) return;
  const id = tr.dataset.id;
  const roles = [...tr.querySelectorAll("input[type=checkbox]:checked")]
    .map((c) => c.value);

  try {
    const res = await fetch(`/api/employees/${encodeURIComponent(id)}/roles`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ roles }),
    });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.error || `HTTP ${res.status}`);
    toast(`Saved ${id} — ${roles.length} role(s).`);
    // Refresh catalog counts in the background.
    loadCatalog();
  } catch (e) {
    toast(`Save failed: ${e.message}`);
  }
}

// ───────── Helpers ─────────
function escapeHtml(s) {
  return String(s)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;");
}
function escapeAttr(s) {
  return escapeHtml(s).replaceAll('"', "&quot;");
}

// ───────── Wire up ─────────
document.addEventListener("DOMContentLoaded", () => {
  if (!session || !session.accessToken) return;

  $("topbar-username").textContent = session.username || session.userId || "";
  $("logout-btn").addEventListener("click", () => {
    sessionStorage.removeItem(STORAGE_KEY);
    window.location.href = "/";
  });

  $("tab-catalog").addEventListener("click", () => showTab("catalog"));
  $("tab-assignment").addEventListener("click", () => showTab("assignment"));

  $("add-form").addEventListener("submit", handleAddRole);
  $("catalog-rows").addEventListener("click", handleCatalogRowClick);
  $("assign-rows").addEventListener("click", handleAssignmentClick);

  $("emp-search").addEventListener("input", (e) => {
    assignState.search = e.target.value;
    assignState.page = 1;
    clearTimeout(searchDebounce);
    searchDebounce = setTimeout(loadEmployees, 200);
  });
  $("emp-pagesize").addEventListener("change", (e) => {
    assignState.pageSize = Number(e.target.value) || 25;
    assignState.page = 1;
    loadEmployees();
  });
  $("page-prev").addEventListener("click", () => {
    if (assignState.page > 1) {
      assignState.page--;
      loadEmployees();
    }
  });
  $("page-next").addEventListener("click", () => {
    assignState.page++;
    loadEmployees();
  });

  loadCatalog();
});
