/* ============================================================ DAWAM — data layer
   Ported from the Claude Design prototype (data.jsx). The mock EMPLOYEES /
   RECON datasets are replaced with a thin live-API layer that talks to the
   HrAuth REST endpoints. The Arabic formatting helpers (avColor, firstLetter,
   day/month names) are kept verbatim so the screens render identically.
   ============================================================ */

/* ---- session (written by the login screen, shared with the legacy pages) -- */
const SESSION_KEY = "hrauth.session";
const RECON_KEY = "hrauth.reconciliation";

function getSession() {
  try { return JSON.parse(sessionStorage.getItem(SESSION_KEY)); }
  catch { return null; }
}
function setSession(s) { sessionStorage.setItem(SESSION_KEY, JSON.stringify(s)); }
function clearSession() { sessionStorage.removeItem(SESSION_KEY); }

/* ---- tiny fetch wrapper. Relative URLs keep us on the current origin so the
        HTTPS-redirect never bounces an API call to an untrusted port. The
        signed-in bearer token is attached automatically from the session, so
        every screen (reports included) authenticates without threading the
        token through by hand. -------------------------------------------------- */
async function api(path, opts = {}) {
  const session = getSession();
  const headers = new Headers(opts.headers || {});
  if (session && session.accessToken && !headers.has("Authorization")) {
    headers.set("Authorization", `Bearer ${session.accessToken}`);
  }
  const res = await fetch(path, { ...opts, headers });
  let data = null;
  try { data = await res.json(); } catch { data = null; }
  if (!res.ok) {
    const msg = (data && (data.error || data.message)) || `HTTP ${res.status}`;
    const err = new Error(msg);
    err.status = res.status;
    err.data = data;
    throw err;
  }
  return data;
}
const apiGet = (path) => api(path);
const apiSend = (path, method, body) =>
  api(path, {
    method,
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

/* ---- avatar colour + initial (verbatim from the prototype) ---------------- */
const AV_COLORS = ["#315c4e", "#b18f5d", "#4f378a", "#5d8ca7", "#a55f5f", "#5e8a6f", "#a87f47", "#6b6f7d"];
function avColor(seed) {
  let h = 0; for (let i = 0; i < (seed || "").length; i++) h = (h * 31 + seed.charCodeAt(i)) | 0;
  return AV_COLORS[Math.abs(h) % AV_COLORS.length];
}
const firstLetter = (name) => (name || "؟").trim().charAt(0);

/* ---- Arabic date formatting (verbatim from the prototype) ----------------- */
const DAY_NAMES_AR = ["الأحد", "الإثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"];
const MONTHS_AR = ["يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"];
function parseD(s) { if (!s) return null; const [y, m, d] = s.split("-").map(Number); return new Date(y, m - 1, d); }
function dayNameAr(s) { const d = parseD(s); return d ? DAY_NAMES_AR[d.getDay()] : ""; }
function longDateAr(s) { const d = parseD(s); if (!d) return s; return `${dayNameAr(s)} ${d.getDate()} ${MONTHS_AR[d.getMonth()]} ${d.getFullYear()}`; }

/* ---- reconciliation result cache (set by Upload, read by Reconciliation) -- */
function getRecon() {
  try { return JSON.parse(sessionStorage.getItem(RECON_KEY)); }
  catch { return null; }
}
function setRecon(r) { sessionStorage.setItem(RECON_KEY, JSON.stringify(r)); }

Object.assign(window, {
  SESSION_KEY, RECON_KEY,
  getSession, setSession, clearSession,
  api, apiGet, apiSend,
  avColor, firstLetter, dayNameAr, longDateAr,
  getRecon, setRecon,
});
