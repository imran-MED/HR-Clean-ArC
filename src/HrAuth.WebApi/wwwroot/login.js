"use strict";

// Login page. On success the session is stored and the browser is
// redirected to the dashboard (main) page.

const STORAGE_KEY = "hrauth.session";
const $ = (id) => document.getElementById(id);

// Already signed in this tab? Skip straight to the dashboard.
if (sessionStorage.getItem(STORAGE_KEY)) {
  window.location.replace("dashboard.html");
}

function resetButton(btn) {
  btn.disabled = false;
  btn.querySelector(".btn-label").textContent = "Sign in";
  btn.querySelector(".spinner").hidden = true;
}

async function handleLogin(event) {
  event.preventDefault();

  const btn = $("login-btn");
  const errorEl = $("login-error");
  errorEl.hidden = true;

  btn.disabled = true;
  btn.querySelector(".btn-label").textContent = "Signing in…";
  btn.querySelector(".spinner").hidden = false;

  try {
    const res = await fetch("/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        userName: $("userName").value.trim(),
        password: $("password").value,
      }),
    });
    const data = await res.json().catch(() => ({}));

    if (res.ok) {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(data));
      // ── Successful login → redirect to the main page. ──
      window.location.href = "dashboard.html";
    } else {
      errorEl.textContent = data.error || `Sign-in failed (${res.status}).`;
      errorEl.hidden = false;
      resetButton(btn);
    }
  } catch {
    errorEl.textContent = "Could not reach the server. Is the API running?";
    errorEl.hidden = false;
    resetButton(btn);
  }
}

document.getElementById("login-form").addEventListener("submit", handleLogin);
