"use strict";

// Dashboard (main) page. Requires a session in sessionStorage — if there
// is none, the browser is redirected back to the login page.

const STORAGE_KEY = "hrauth.session";
const $ = (id) => document.getElementById(id);

let session = null;
try {
  session = JSON.parse(sessionStorage.getItem(STORAGE_KEY));
} catch {
  session = null;
}

// Guard: not signed in → back to login.
if (!session || !session.accessToken) {
  window.location.replace("/");
}

// ───────── Rendering ─────────
function render() {
  const roles = session.roles || [];
  const isAdmin = roles.some((r) => r.toLowerCase().includes("admin"));
  const name = session.displayName || session.username || session.userId || "User";

  $("topbar-username").textContent = session.username || session.userId || "";
  $("welcome-name").textContent = name;
  $("welcome-sub").textContent = `Signed in · Employee ID ${session.userId || "—"}`;
  $("admin-badge").hidden = !isAdmin;

  $("kv-userid").textContent = session.userId || "—";
  $("kv-username").textContent = session.username || "—";
  $("kv-name").textContent = session.displayName || "—";

  const rolesEl = $("kv-roles");
  rolesEl.innerHTML = "";
  if (roles.length === 0) {
    rolesEl.textContent = "No roles";
  } else {
    roles.forEach((r) => {
      const span = document.createElement("span");
      span.className = "pill pill-role";
      span.textContent = r;
      rolesEl.appendChild(span);
    });
  }

  const expires = session.expiresAt ? new Date(session.expiresAt) : null;
  const expValid = expires && !isNaN(expires.getTime());
  $("kv-expires").textContent = expValid ? expires.toLocaleString() : "Not reported";

  const statusEl = $("kv-status");
  if (expValid) {
    const expired = expires.getTime() <= Date.now();
    statusEl.textContent = expired ? "Expired" : "Active";
    statusEl.className = "pill " + (expired ? "pill-bad" : "pill-ok");
  } else {
    statusEl.textContent = "Unknown";
    statusEl.className = "pill";
  }

  $("token-box").textContent = session.accessToken || "—";
}

// ───────── Employee details (position / supervisor) ─────────
// These aren't in the login token — fetch the directory record by ID.
async function loadEmployeeDetails() {
  const id = session.userId;
  if (!id) return;
  try {
    const res = await fetch(`/api/employees/${encodeURIComponent(id)}`);
    if (!res.ok) return; // 404 = not in directory yet; leave the "—" placeholders
    const e = await res.json();
    $("kv-position").textContent = e.position || "—";
    if (e.supervisorName) {
      $("kv-supervisor").textContent = e.supervisorEmployeeId
        ? `${e.supervisorName} (${e.supervisorEmployeeId})`
        : e.supervisorName;
    } else {
      $("kv-supervisor").textContent = e.supervisorEmployeeId || "—";
    }
  } catch {
    /* network error — placeholders stay as "—" */
  }
}

// ───────── Toast ─────────
let toastTimer;
function toast(message) {
  const el = $("toast");
  el.textContent = message;
  el.hidden = false;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { el.hidden = true; }, 2800);
}

// ───────── Actions ─────────
function handleLogout() {
  sessionStorage.removeItem(STORAGE_KEY);
  window.location.href = "/";
}

async function handleValidate() {
  try {
    const res = await fetch("/api/auth/validate-token", {
      method: "POST",
      headers: { Authorization: `Bearer ${session.accessToken}` },
    });
    const data = await res.json().catch(() => ({}));
    toast(data.valid ? "Token is valid ✓" : "Token is no longer valid ✗");
  } catch {
    toast("Could not reach the server.");
  }
}

async function handleRefresh() {
  if (!session.refreshToken) {
    toast("No refresh token was issued for this session.");
    return;
  }
  try {
    const res = await fetch("/api/auth/refresh-token", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        token: session.accessToken,
        refreshToken: session.refreshToken,
      }),
    });
    const data = await res.json().catch(() => ({}));
    if (res.ok) {
      session = data;
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(data));
      render();
      toast("Token refreshed ✓");
    } else {
      toast(data.error || "Refresh failed.");
    }
  } catch {
    toast("Could not reach the server.");
  }
}

async function handleCopy() {
  try {
    await navigator.clipboard.writeText(session.accessToken || "");
    toast("Access token copied to clipboard");
  } catch {
    toast("Copy failed — select the token manually.");
  }
}

// ───────── Wire up ─────────
document.addEventListener("DOMContentLoaded", () => {
  if (!session || !session.accessToken) return; // redirect already in flight

  $("logout-btn").addEventListener("click", handleLogout);
  $("validate-btn").addEventListener("click", handleValidate);
  $("refresh-btn").addEventListener("click", handleRefresh);
  $("copy-btn").addEventListener("click", handleCopy);

  render();
  loadEmployeeDetails();
});
