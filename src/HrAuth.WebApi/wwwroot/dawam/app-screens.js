/* ============================================================ DAWAM — Roster / Upload / Employees / Roles
   Ported from screens.jsx, wired to the live HrAuth REST API. The Employees
   screen ADDS the Position + Supervisor fields the design omitted (directory
   columns + add form), backed by /api/employees position/supervisorEmployeeId/
   supervisorName. */
const { useState, useEffect, useRef } = React;

/* tiny toast (the design has no toast component; this is a lightweight one) */
let _toastTimer;
function toast(msg) {
  let el = document.getElementById("dawam-toast");
  if (!el) {
    el = document.createElement("div");
    el.id = "dawam-toast";
    el.style.cssText =
      "position:fixed;bottom:24px;left:50%;transform:translateX(-50%);"
      + "background:var(--color-text);color:#fff;padding:11px 18px;border-radius:8px;"
      + "font-family:var(--font-ar);font-size:13.5px;box-shadow:var(--shadow-menu);"
      + "z-index:9999;opacity:0;transition:opacity .18s;max-width:90vw;text-align:center;";
    document.body.appendChild(el);
  }
  el.textContent = msg;
  el.style.opacity = "1";
  clearTimeout(_toastTimer);
  _toastTimer = setTimeout(() => { el.style.opacity = "0"; }, 2600);
}

/* ---------------------------------------------------------------- Roster */
function RosterScreen() {
  const session = getSession() || {};
  const today = (() => {
    const n = new Date();
    return `${n.getFullYear()}-${String(n.getMonth() + 1).padStart(2, "0")}-${String(n.getDate()).padStart(2, "0")}`;
  })();
  const [empId, setEmpId] = useState(session.userId || "");
  const [date, setDate] = useState(today);
  const [data, setData] = useState(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  async function fetchRoster(id, d) {
    const eid = (id ?? empId).trim();
    if (!eid) { setError("أدخل الرقم الوظيفي."); setData(null); return; }
    setLoading(true); setError(""); setData(null);
    try {
      const url = `/api/roster/${encodeURIComponent(eid)}` + (d || date ? `?date=${encodeURIComponent(d || date)}` : "");
      setData(await apiGet(url));
    } catch (e) {
      setError(e.message || "تعذّر جلب الوردية.");
    } finally {
      setLoading(false);
    }
  }
  // Auto-load the signed-in employee's roster on first open.
  useEffect(() => { if (session.userId) fetchRoster(session.userId, today); }, []);

  const totalHours = data && data.periods
    ? data.periods.reduce((s, p) => s + (p.requiredHours || 0), 0) : 0;

  return (
    <React.Fragment>
      <PageHead title="جداول الدوام" desc="عرض وردية الموظف وفتراته المجدولة، المجلوبة من خدمة جداول الدوام (SmartHCM)." />
      <div className="card">
        <div className="card-head"><h2>بحث عن وردية</h2></div>
        <div className="formrow">
          <div className="field"><label>الرقم الوظيفي</label><input value={empId} onChange={(e) => setEmpId(e.target.value)} className="lat" /></div>
          <div className="field"><label>التاريخ</label><input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></div>
          <button className="btn btn-primary" disabled={loading} onClick={() => fetchRoster()}>
            <i className={"ti " + (loading ? "ti-loader-2" : "ti-search")} style={loading ? { animation: "spin .7s linear infinite" } : null}></i>جلب الوردية
          </button>
        </div>
        {error && <p className="error" style={{ color: "var(--color-danger-dark)", marginTop: 12 }}>{error}</p>}
      </div>

      {data && !data.hasRoster && (
        <div className="card"><p className="muted" style={{ margin: 0 }}>لا توجد وردية مسجّلة للموظف <span className="lat">{data.employeeId}</span> بتاريخ <span className="lat">{data.date}</span>.</p></div>
      )}

      {data && data.hasRoster && (
        <div className="card">
          <div className="card-head">
            <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
              <span className="chip chip-primary"><span className="dot"></span>{data.shiftName || "وردية"}</span>
              <span className="lat" style={{ fontWeight: 700, fontSize: 15 }}>{data.shiftStart} – {data.shiftEnd}</span>
            </div>
            <span className="sub">
              <span className="emp-cell" style={{ display: "inline-flex" }}>
                <div className="av" style={{ background: avColor(data.employeeId), width: 26, height: 26, fontSize: 11 }}>{firstLetter(data.employeeId)}</div>
                <span className="lat">{data.employeeId}</span> · {data.date}
              </span>
            </span>
          </div>
          <div className="table-wrap">
            <table className="data">
              <thead><tr><th>#</th><th>الفترة</th><th>البداية</th><th>النهاية</th><th>الساعات المطلوبة</th></tr></thead>
              <tbody>
                {data.periods.map((p, i) => (
                  <tr key={i}>
                    <td className="lat">{i + 1}</td>
                    <td>{p.name}</td>
                    <td className="lat">{p.startTime}</td>
                    <td className="lat">{p.endTime}</td>
                    <td><span className="lat">{p.requiredHours}</span> ساعات</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="hr" style={{ margin: "16px 0" }}></div>
          <div style={{ display: "flex", gap: 24, fontSize: 13, color: "var(--color-text-60)" }}>
            <span><i className="ti ti-clock" style={{ verticalAlign: "-3px", marginInlineStart: 4 }}></i> إجمالي ساعات الوردية: <b style={{ color: "var(--color-text-90)" }}><span className="lat">{totalHours}</span> ساعات</b></span>
          </div>
        </div>
      )}
    </React.Fragment>
  );
}

/* ---------------------------------------------------------------- Upload */
/* Status value that means "check-in" (دخول); mirrors the server setting
   AttendanceReconciliation:CheckInStatus. The opposite value is check-out. */
const CHECKIN_STATUS = 1;

/* Left-pad an employee ID to the canonical 8 digits (7 → 8). */
function pad8(id) {
  const s = String(id == null ? "" : id).trim();
  return s.length >= 8 ? s : s.padStart(8, "0");
}

/* A server punch record → the editable row shape used by the Upload screen. */
function recToEditable(r) {
  const ts = String(r.timestamp || "");
  let time = ts.replace("T", " ").slice(11, 19);
  if (time.length === 5) time += ":00";
  if (!time) time = "00:00:00";
  return {
    rawEmployeeId: r.rawEmployeeId || r.employeeId || "",
    employeeId: pad8(r.employeeId || r.rawEmployeeId || ""),
    date: ts.slice(0, 10),
    time,
    status: typeof r.status === "number" ? r.status : 0,
    verify: r.verify == null ? 1 : r.verify,
    workCode: r.workCode == null ? 0 : r.workCode,
    reserved: r.reserved == null ? 0 : r.reserved,
  };
}

/* Editable rows → machine-export text (8-digit IDs, space-separated columns). */
function recordsToText(records) {
  return records
    .filter((r) => r.employeeId && r.date && r.time)
    .map((r) => [pad8(r.employeeId), r.date, r.time, r.status, r.verify, r.workCode, r.reserved].join(" "))
    .join("\n");
}

/* Trigger a browser download of plain text. */
function downloadText(filename, text) {
  const blob = new Blob([text], { type: "text/plain;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

function UploadScreen({ onNav }) {
  const fileRef = useRef(null);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState(getRecon());
  const [records, setRecords] = useState(() => {
    const r = getRecon();
    return r && Array.isArray(r.records) ? r.records.map(recToEditable) : [];
  });
  const [error, setError] = useState("");
  const [dirty, setDirty] = useState(false);
  const [onlyIssues, setOnlyIssues] = useState(false);
  const [page, setPage] = useState(1);
  const pageSize = 50;

  // Punch keys (employee|date|HH:mm) the reconciler flagged as a wrong in/out.
  // Derived context from the last reconciliation: employee name/position, the
  // day's shift + period breakups, and the written issue note per punch.
  const meta = React.useMemo(() => {
    const byEmpDate = new Map();   // "emp|date" -> day object
    const noteByKey = new Map();   // "emp|date|HH:mm" -> directionNote
    ((result && result.days) || []).forEach((d) => {
      byEmpDate.set(`${d.employeeId}|${d.date}`, d);
      (d.periods || []).forEach((p) => {
        if (p.hasDirectionIssue && p.directionNote) {
          if (p.firstPunch) noteByKey.set(`${d.employeeId}|${d.date}|${p.firstPunch}`, p.directionNote);
          if (p.lastPunch) noteByKey.set(`${d.employeeId}|${d.date}|${p.lastPunch}`, p.directionNote);
        }
      });
    });
    return { byEmpDate, noteByKey };
  }, [result]);

  const recDay = (r) => meta.byEmpDate.get(`${pad8(r.employeeId)}|${r.date}`);
  const recNote = (r) =>
    meta.noteByKey.get(`${pad8(r.employeeId)}|${r.date}|${(r.time || "").slice(0, 5)}`);
  const recIsSuspect = (r) => !!recNote(r);

  // "man8 — man8-b1 05:00–09:00 · man8-b2 16:15–20:30"
  const shiftBreakups = (day) =>
    day && day.periods
      ? day.periods.map((p) => `${p.name} ${p.startTime}–${p.endTime}`).join(" · ")
      : "";

  async function onFile(file) {
    if (!file) return;
    setBusy(true); setError("");
    try {
      const form = new FormData();
      form.append("file", file);
      const res = await fetch("/api/attendance/upload", { method: "POST", body: form });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(data.error || `فشل الرفع (${res.status}).`);
      setRecon(data); setResult(data);
      setRecords((data.records || []).map(recToEditable));
      setDirty(false);
    } catch (e) {
      setError(e.message || "تعذّر الاتصال بالخادم.");
    } finally {
      setBusy(false);
    }
  }

  function editRec(i, patch) {
    setRecords((rs) => rs.map((r, j) => (j === i ? { ...r, ...patch } : r)));
    setDirty(true);
  }
  function delRec(i) { setRecords((rs) => rs.filter((_, j) => j !== i)); setDirty(true); }
  function addRec() {
    const base = records[0] || {};
    setRecords((rs) => [...rs, {
      rawEmployeeId: "", employeeId: "", date: base.date || "",
      time: "08:00:00", status: CHECKIN_STATUS, verify: 1, workCode: 0, reserved: 0,
    }]);
    setDirty(true);
  }

  async function reReconcile() {
    setBusy(true); setError("");
    try {
      const text = recordsToText(records);
      if (!text) throw new Error("لا توجد بصمات لإعادة الحساب.");
      const data = await apiSend("/api/attendance/reconcile", "POST", {
        fileName: (result && result.fileName) || "corrected.txt", content: text,
      });
      setRecon(data); setResult(data);
      setRecords((data.records || []).map(recToEditable));
      setDirty(false);
    } catch (e) {
      setError(e.message || "تعذّر إعادة الحساب.");
    } finally {
      setBusy(false);
    }
  }

  function download() {
    const text = recordsToText(records);
    if (!text) { setError("لا توجد بصمات للتنزيل."); return; }
    const base = ((result && result.fileName) || "attendance").replace(/\.[^.]+$/, "");
    downloadText(`${base}-corrected.txt`, text + "\n");
  }

  const suspectCount = records.filter(recIsSuspect).length;

  // Paginate (and optionally show only flagged rows) so the table never renders
  // thousands of editable rows at once — that froze the browser on big files.
  const indexed = records.map((r, i) => ({ r, i }));
  const filtered = onlyIssues ? indexed.filter(({ r }) => recIsSuspect(r)) : indexed;
  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const curPage = Math.min(page, totalPages);
  const pageItems = filtered.slice((curPage - 1) * pageSize, curPage * pageSize);

  return (
    <React.Fragment>
      <PageHead title="رفع البصمات" desc="ارفع ملف البصمات ثم راجع وصحّح الإدخالات الخاطئة (دخول/خروج) قبل تنزيل الملف المصحَّح. تُوحَّد الأرقام الوظيفية إلى ٨ خانات." />
      <div className="card">
        <div className="card-head"><h2>اختيار الملف</h2></div>
        <p style={{ fontSize: 13, color: "var(--color-text-60)", margin: "0 0 14px", textAlign: "start" }}>
          صيغة السطر المتوقعة: <span className="mono">EmployeeId Date Time Status Verify WorkCode Reserved</span>
          {" "}— مثال: <span className="mono">0002147 2026-06-07 07:26:11 1 0 1 0</span>
        </p>
        <input ref={fileRef} type="file" accept=".dat,.txt,.log,.csv" style={{ display: "none" }}
          onChange={(e) => onFile(e.target.files[0])} />
        <div className="dropzone" onClick={() => fileRef.current && fileRef.current.click()} style={{ cursor: "pointer" }}>
          <div className="ic"><i className={"ti " + (busy ? "ti-loader-2" : "ti-cloud-upload")} style={busy ? { animation: "spin .7s linear infinite" } : null}></i></div>
          <div className="t">{busy ? "جارٍ المعالجة..." : "اسحب ملف البصمات هنا أو اضغط للاختيار"}</div>
          <div className="s">يدعم الصيغ <span className="mono">.dat</span> <span className="mono">.txt</span> <span className="mono">.log</span> <span className="mono">.csv</span></div>
          <button className="btn btn-primary" style={{ marginTop: 6 }} disabled={busy}><i className="ti ti-file-upload"></i>اختيار ملف</button>
        </div>
        {error && <p className="error" style={{ color: "var(--color-danger-dark)", marginTop: 12 }}>{error}</p>}
      </div>

      {result && (
        <div className="card">
          <div className="card-head">
            <h2>البصمات — راجع وعدّل قبل التنزيل <span className="mono">{result.fileName}</span></h2>
            <button className="btn btn-soft btn-sm" onClick={addRec}><i className="ti ti-plus"></i>إضافة بصمة</button>
          </div>
          <div className="stat-row" style={{ gridTemplateColumns: "repeat(4,1fr)", marginBottom: 18 }}>
            <div className="stat-tile"><div className="ic ic-primary"><i className="ti ti-list-numbers"></i></div><div className="meta"><div className="num">{records.length}</div><div className="lbl">بصمة</div></div></div>
            <div className="stat-tile"><div className="ic ic-success"><i className="ti ti-checks"></i></div><div className="meta"><div className="num">{result.importedCount}</div><div className="lbl">بصمة مستوردة</div></div></div>
            <div className="stat-tile"><div className="ic ic-warning"><i className="ti ti-alert-triangle"></i></div><div className="meta"><div className="num">{suspectCount}</div><div className="lbl">إدخالات مشكوك فيها</div></div></div>
            <div className="stat-tile"><div className="ic ic-danger"><i className="ti ti-file-alert"></i></div><div className="meta"><div className="num">{result.skippedCount}</div><div className="lbl">سطر متجاهَل</div></div></div>
          </div>

          {suspectCount > 0 && (
            <p className="muted small" style={{ margin: "0 0 12px", color: "var(--color-warning-dark, #8a6d00)" }}>
              <i className="ti ti-alert-triangle" style={{ verticalAlign: "-2px", marginInlineStart: 4 }}></i>
              {" "}الصفوف المظلّلة بها مشكلة في نوع البصمة (مثل خروج مُسجَّل عند بداية الوردية) — صحّح «النوع» أو «الوقت» ثم أعد الحساب.
            </p>
          )}

          <div style={{ display: "flex", gap: 12, alignItems: "center", flexWrap: "wrap", margin: "0 0 12px" }}>
            <label style={{ display: "inline-flex", alignItems: "center", gap: 6, fontSize: 13, cursor: "pointer" }}>
              <input type="checkbox" checked={onlyIssues} onChange={(e) => { setOnlyIssues(e.target.checked); setPage(1); }} />
              عرض الإدخالات المشكوك فيها فقط ({suspectCount})
            </label>
            <span className="muted small" style={{ marginInlineStart: "auto" }}>
              عرض <span className="lat">{filtered.length === 0 ? 0 : (curPage - 1) * pageSize + 1}</span>–
              <span className="lat">{Math.min(curPage * pageSize, filtered.length)}</span> من <span className="lat">{filtered.length}</span>
            </span>
            <div style={{ display: "inline-flex", gap: 6 }}>
              <button className="btn btn-ghost btn-sm" disabled={curPage <= 1} onClick={() => setPage(curPage - 1)}><i className="ti ti-chevron-right"></i>السابق</button>
              <span className="lat small" style={{ alignSelf: "center" }}>{curPage}/{totalPages}</span>
              <button className="btn btn-ghost btn-sm" disabled={curPage >= totalPages} onClick={() => setPage(curPage + 1)}>التالي<i className="ti ti-chevron-left"></i></button>
            </div>
          </div>

          <div className="table-wrap">
            <table className="data">
              <thead><tr>
                <th>#</th><th>الرقم الموحَّد (٨)</th><th>الموظف</th><th>المنصب</th>
                <th>التاريخ</th><th>الوقت</th><th>النوع</th>
                <th>الوردية والفترات</th><th>الملاحظة</th><th></th>
              </tr></thead>
              <tbody>
                {pageItems.length === 0 ? (
                  <tr><td colSpan={10} className="empty-row">{records.length === 0 ? "لا توجد بصمات — ارفع ملفاً أعلاه." : "لا توجد إدخالات مطابقة."}</td></tr>
                ) : pageItems.map(({ r, i }) => {
                  const note = recNote(r);
                  const suspect = !!note;
                  const isIn = r.status === CHECKIN_STATUS;
                  const day = recDay(r);
                  return (
                    <tr key={i} className={suspect ? "row-warning" : ""}>
                      <td className="lat">
                        {i + 1}
                        {suspect && <i className="ti ti-alert-triangle" title="بصمة مشكوك فيها" style={{ color: "var(--color-warning, #d39e00)", marginInlineStart: 4 }}></i>}
                      </td>
                      <td>
                        <input className="lat" value={r.employeeId} style={{ width: 92, padding: "5px 8px" }}
                          onChange={(e) => editRec(i, { employeeId: e.target.value })}
                          onBlur={(e) => editRec(i, { employeeId: pad8(e.target.value) })} />
                        {r.rawEmployeeId && r.rawEmployeeId !== r.employeeId &&
                          <div className="lat muted small">من: {r.rawEmployeeId}</div>}
                      </td>
                      <td dir="auto">{(day && day.employeeName) || <span className="muted">—</span>}</td>
                      <td dir="auto">{(day && day.position) || <span className="muted">—</span>}</td>
                      <td><input type="date" value={r.date} onChange={(e) => editRec(i, { date: e.target.value })} /></td>
                      <td>
                        <input type="time" step="1" value={r.time}
                          onChange={(e) => editRec(i, { time: e.target.value.length === 5 ? e.target.value + ":00" : e.target.value })} />
                      </td>
                      <td>
                        <select value={isIn ? "in" : "out"}
                          onChange={(e) => editRec(i, { status: e.target.value === "in" ? CHECKIN_STATUS : 0 })}>
                          <option value="in">دخول</option>
                          <option value="out">خروج</option>
                        </select>
                      </td>
                      <td style={{ minWidth: 200 }}>
                        {day && day.hasRoster ? (
                          <React.Fragment>
                            <span className="chip chip-primary" style={{ padding: "3px 9px" }}><span className="dot"></span>{day.shiftName || "وردية"}</span>
                            <div className="lat muted small" style={{ marginTop: 3 }}>{shiftBreakups(day)}</div>
                          </React.Fragment>
                        ) : <span className="muted">بدون وردية</span>}
                      </td>
                      <td style={{ minWidth: 170 }}>
                        {note
                          ? <span className="pill pill-warn" style={{ whiteSpace: "normal" }}>{note}</span>
                          : <span className="muted">—</span>}
                      </td>
                      <td><button className="btn btn-danger btn-sm" title="حذف" onClick={() => delRec(i)}><i className="ti ti-trash"></i></button></td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          <div style={{ display: "flex", gap: 10, marginTop: 16, flexWrap: "wrap" }}>
            <button className={"btn btn-primary" + (dirty ? " dirty" : "")} disabled={busy || !dirty} onClick={reReconcile}>
              <i className={"ti " + (busy ? "ti-loader-2" : "ti-refresh")} style={busy ? { animation: "spin .7s linear infinite" } : null}></i>إعادة الحساب
            </button>
            <button className="btn btn-soft" disabled={busy || records.length === 0} onClick={download}><i className="ti ti-download"></i>تنزيل الملف المصحَّح</button>
            <button className="btn btn-ghost" onClick={() => onNav("reconciliation")}><i className="ti ti-report-analytics"></i>عرض تسوية الحضور</button>
          </div>
          {dirty && (
            <p className="muted small" style={{ marginTop: 8 }}>
              لديك تعديلات غير محسوبة — اضغط «إعادة الحساب» لتحديث التأخير والأعلام، أو «تنزيل الملف المصحَّح» لحفظ النتيجة.
            </p>
          )}
        </div>
      )}
    </React.Fragment>
  );
}

/* ---------------------------------------------------------------- Employees */
function supervisorCell(e) {
  if (e.supervisorName) {
    return <React.Fragment><span dir="auto">{e.supervisorName}</span> <span className="lat muted">({e.supervisorEmployeeId})</span></React.Fragment>;
  }
  if (e.supervisorEmployeeId) return <span className="id-badge">{e.supervisorEmployeeId}</span>;
  return <span className="muted">—</span>;
}

function EmployeesScreen() {
  const [tab, setTab] = useState("dir");
  const pageSize = 12;
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [rows, setRows] = useState([]);
  const [search, setSearch] = useState("");
  const [posFilter, setPosFilter] = useState("");
  const [posList, setPosList] = useState([]);
  const [status, setStatus] = useState("جارٍ التحميل…");
  const [err, setErr] = useState("");

  // add form
  const [aId, setAId] = useState("");
  const [aName, setAName] = useState("");
  const [aUser, setAUser] = useState("");
  const [aPos, setAPos] = useState("");
  const [aSup, setASup] = useState("");
  const [adding, setAdding] = useState(false);
  const [addErr, setAddErr] = useState("");

  // import
  const impRef = useRef(null);
  const [impResult, setImpResult] = useState(null);
  const [impErr, setImpErr] = useState("");
  const [impBusy, setImpBusy] = useState(false);

  const pages = Math.max(1, Math.ceil(total / pageSize));

  async function load() {
    setStatus("جارٍ التحميل…"); setErr("");
    try {
      const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
      if (search) params.set("search", search);
      if (posFilter) params.set("position", posFilter);
      const data = await apiGet(`/api/employees?${params}`);
      setRows(data.items); setTotal(data.total);
      const start = (data.page - 1) * data.pageSize + 1;
      const end = Math.min(start + data.items.length - 1, data.total);
      setStatus(data.total === 0 ? "لا توجد نتائج." : `عرض ${start}–${end} من ${data.total}`);
    } catch (e) {
      setErr(`تعذّر التحميل: ${e.message}`); setStatus("");
    }
  }
  useEffect(() => { load(); }, [page]);
  // debounce search
  useEffect(() => {
    const t = setTimeout(() => { page === 1 ? load() : setPage(1); }, 220);
    return () => clearTimeout(t);
  }, [search]);
  // position filter changes reload immediately (reset to first page)
  useEffect(() => { page === 1 ? load() : setPage(1); }, [posFilter]);
  // distinct positions for the filter dropdown
  function loadPositions() {
    apiGet("/api/employees/positions").then(setPosList).catch(() => {});
  }
  useEffect(loadPositions, []);

  async function addEmployee(e) {
    e.preventDefault();
    setAddErr("");
    if (!aId.trim() || !aName.trim()) { setAddErr("الرقم الوظيفي والاسم مطلوبان."); return; }
    setAdding(true);
    try {
      const data = await apiSend("/api/employees", "POST", {
        employeeId: aId.trim(),
        displayName: aName.trim(),
        userName: aUser.trim() || null,
        position: aPos.trim() || null,
        supervisorEmployeeId: aSup.trim() || null,
      });
      toast(`تمت إضافة ${data.employeeId} — ${data.displayName}`);
      setAId(""); setAName(""); setAUser(""); setAPos(""); setASup("");
      setSearch(data.employeeId);
    } catch (e) {
      setAddErr(e.message);
    } finally {
      setAdding(false);
    }
  }

  async function syncEmp(id) {
    try {
      const data = await apiSend(`/api/employees/${encodeURIComponent(id)}/sync-from-smarthcm`, "POST", {});
      toast(`تمت مزامنة ${data.employeeId} من SmartHCM`);
      load();
      loadPositions();
    } catch (e) {
      toast(`فشل المزامنة: ${e.message}`);
    }
  }

  async function onImport(file) {
    if (!file) return;
    setImpErr(""); setImpResult(null);
    try {
      const form = new FormData();
      form.append("file", file);
      const res = await fetch("/api/employees/import-csv", { method: "POST", body: form });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(data.error || `فشل الاستيراد (${res.status}).`);
      setImpResult(data);
      load();
      loadPositions();
    } catch (e) {
      setImpErr(e.message);
    }
  }

  // Import the whole directory straight from the HR API (no file). The
  // signed-in user's token is forwarded; the HR API enforces directory-read
  // authorization. Positions are only written when the HR API reports one.
  async function onImportFromApi() {
    setImpErr(""); setImpResult(null); setImpBusy(true);
    try {
      const data = await api("/api/employees/import-from-hrapi", { method: "POST" });
      setImpResult(data);
      load();
      loadPositions();
    } catch (e) {
      setImpErr(e.message);
    } finally {
      setImpBusy(false);
    }
  }

  return (
    <React.Fragment>
      <PageHead title="الموظفون" desc="استعراض الموظفين أو إضافتهم أو استيرادهم، ليتمكّن النظام من ربط بصمات الحضور بأسماء الموظفين.">
        <span className="chip chip-primary" style={{ padding: "6px 12px" }}><i className="ti ti-users" style={{ fontSize: 15 }}></i> <span className="lat">{total}</span> في الدليل</span>
      </PageHead>
      <div className="card">
        <div className="tabs">
          <button className={"tab" + (tab === "dir" ? " active" : "")} onClick={() => setTab("dir")}>الدليل</button>
          <button className={"tab" + (tab === "imp" ? " active" : "")} onClick={() => setTab("imp")}>استيراد CSV</button>
        </div>

        {tab === "dir" ? (
          <div className="fade-in">
            <div className="card-head"><h2>إضافة موظف جديد</h2></div>
            <form className="formrow" style={{ marginBottom: 22 }} onSubmit={addEmployee}>
              <div className="field"><label>الرقم الوظيفي</label><input value={aId} onChange={(e) => setAId(e.target.value)} placeholder="أرقام — تُكمَّل إلى ٨ خانات" className="lat" /></div>
              <div className="field"><label>الاسم الكامل</label><input value={aName} onChange={(e) => setAName(e.target.value)} placeholder="الاسم بالعربية" /></div>
              <div className="field"><label>اسم المستخدم (اختياري)</label><input value={aUser} onChange={(e) => setAUser(e.target.value)} className="lat" placeholder="username" /></div>
              <div className="field"><label>المنصب (اختياري)</label><input value={aPos} onChange={(e) => setAPos(e.target.value)} placeholder="المسمّى الوظيفي" /></div>
              <div className="field"><label>رقم المسؤول المباشر (اختياري)</label><input value={aSup} onChange={(e) => setASup(e.target.value)} className="lat" placeholder="الرقم الوظيفي للمسؤول" /></div>
              <button className="btn btn-primary" type="submit" disabled={adding}>
                <i className={"ti " + (adding ? "ti-loader-2" : "ti-user-plus")} style={adding ? { animation: "spin .7s linear infinite" } : null}></i>إضافة موظف
              </button>
            </form>
            {addErr && <p className="error" style={{ color: "var(--color-danger-dark)", margin: "-10px 0 18px" }}>{addErr}</p>}

            <div className="card-head"><h2>الدليل</h2></div>
            <div className="formrow" style={{ marginBottom: 14 }}>
              <div className="field" style={{ maxWidth: 320 }}>
                <label>بحث</label>
                <div className="in-icon"><i className="ti ti-search"></i><input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="الرقم الوظيفي أو الاسم أو اسم المستخدم" /></div>
              </div>
              <div className="field" style={{ maxWidth: 260 }}>
                <label>المنصب</label>
                <select value={posFilter} onChange={(e) => setPosFilter(e.target.value)}>
                  <option value="">كل المناصب</option>
                  {posList.map((p) => <option key={p} value={p}>{p}</option>)}
                </select>
              </div>
            </div>
            <p className="muted small" style={{ margin: "0 0 8px" }}>{err ? <span style={{ color: "var(--color-danger-dark)" }}>{err}</span> : status}</p>
            <div className="table-wrap">
              <table className="data">
                <thead><tr><th>الرقم الوظيفي</th><th>الاسم</th><th>اسم المستخدم</th><th>المنصب</th><th>المسؤول المباشر</th><th>الأدوار</th><th></th></tr></thead>
                <tbody>
                  {rows.length === 0 ? (
                    <tr><td colSpan={7} className="empty-row">لا يوجد موظفون.</td></tr>
                  ) : rows.map((e) => (
                    <tr key={e.employeeId} className="clickable">
                      <td><span className="id-badge">{e.employeeId}</span></td>
                      <td><div className="emp-cell"><div className="av" style={{ background: avColor(e.employeeId) }}>{firstLetter(e.displayName)}</div><span dir="auto">{e.displayName || "—"}</span></div></td>
                      <td className="lat muted">{e.userName || "—"}</td>
                      <td dir="auto">{e.position || <span className="muted">—</span>}</td>
                      <td>{supervisorCell(e)}</td>
                      <td><div className="roles-wrap">{(e.roles || []).length ? e.roles.map((r) => <span key={r} className="tag">{r}</span>) : <span className="muted">—</span>}</div></td>
                      <td><button className="btn btn-soft btn-sm" title="مزامنة المنصب والمسؤول من SmartHCM" onClick={() => syncEmp(e.employeeId)}><i className="ti ti-refresh"></i></button></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="pager">
              <button className="btn btn-ghost btn-sm" disabled={page <= 1} onClick={() => setPage(page - 1)}><i className="ti ti-chevron-right"></i>السابق</button>
              <span className="lbl">صفحة <span className="lat">{page}</span> من <span className="lat">{pages}</span></span>
              <button className="btn btn-ghost btn-sm" disabled={page >= pages} onClick={() => setPage(page + 1)}>التالي<i className="ti ti-chevron-left"></i></button>
            </div>
          </div>
        ) : (
          <div className="fade-in">
            <div className="card-head"><h2>رفع ملف الدليل (CSV)</h2></div>
            <p style={{ fontSize: 13, color: "var(--color-text-60)", margin: "0 0 16px", textAlign: "start" }}>
              الصيغ المقبولة: <span className="mono">ID,Name</span> · <span className="mono">ID⇥Name</span> · <span className="mono">ID;Name</span>. ترميز UTF-8 فقط. يُتخطّى صف العناوين تلقائياً. تُكمَّل الأرقام إلى ٨ خانات.
            </p>
            <input ref={impRef} type="file" accept=".csv,.tsv,.txt" style={{ display: "none" }} onChange={(e) => onImport(e.target.files[0])} />
            <div className="dropzone" onClick={() => impRef.current && impRef.current.click()} style={{ cursor: "pointer" }}>
              <div className="ic"><i className="ti ti-file-spreadsheet"></i></div>
              <div className="t">اسحب ملف CSV هنا</div>
              <div className="s">الموظفون الحاليون يحتفظون باسم المستخدم والأدوار — يُحدَّث الاسم فقط</div>
              <button className="btn btn-primary" style={{ marginTop: 6 }}><i className="ti ti-upload"></i>اختيار ملف</button>
            </div>

            <div className="hr" style={{ margin: "18px 0" }}></div>
            <div className="card-head"><h2>أو الاستيراد من واجهة الموارد البشرية</h2></div>
            <p style={{ fontSize: 13, color: "var(--color-text-60)", margin: "0 0 14px", textAlign: "start" }}>
              يجلب جميع الموظفين (الرقم الوظيفي، الاسم، المنصب) مباشرة من HR API — دون ملف. لا يُمسح المنصب المخزَّن إذا لم تُبلّغ الواجهة عنه.
            </p>
            <button className="btn btn-primary" disabled={impBusy} onClick={onImportFromApi}>
              <i className={"ti " + (impBusy ? "ti-loader-2" : "ti-cloud-download")}
                 style={impBusy ? { animation: "spin .7s linear infinite" } : null}></i>
              {impBusy ? "جارٍ الاستيراد..." : "استيراد من HR API"}
            </button>

            {impErr && <p className="error" style={{ color: "var(--color-danger-dark)", marginTop: 12 }}>{impErr}</p>}
            {impResult && (
              <div className="stat-row" style={{ gridTemplateColumns: "repeat(4,1fr)", marginTop: 18 }}>
                <div className="stat-tile"><div className="ic ic-primary"><i className="ti ti-list-numbers"></i></div><div className="meta"><div className="num">{impResult.linesParsed}</div><div className="lbl">سطر مقروء</div></div></div>
                <div className="stat-tile"><div className="ic ic-success"><i className="ti ti-checks"></i></div><div className="meta"><div className="num">{impResult.rowsImported}</div><div className="lbl">صف مكتوب</div></div></div>
                <div className="stat-tile"><div className="ic ic-danger"><i className="ti ti-file-alert"></i></div><div className="meta"><div className="num">{impResult.rowsSkipped}</div><div className="lbl">صف متجاهَل</div></div></div>
                <div className="stat-tile"><div className="ic ic-info"><i className="ti ti-users"></i></div><div className="meta"><div className="num">{impResult.totalEmployeesInDb}</div><div className="lbl">الإجمالي في القاعدة</div></div></div>
              </div>
            )}
          </div>
        )}
      </div>
    </React.Fragment>
  );
}

/* ---------------------------------------------------------------- Roles */
function RolesScreen() {
  const [tab, setTab] = useState("cat");
  const [catalog, setCatalog] = useState([]);
  const [catErr, setCatErr] = useState("");
  const [rName, setRName] = useState("");
  const [rDesc, setRDesc] = useState("");

  // assignment
  const [emps, setEmps] = useState([]);
  const [aSearch, setASearch] = useState("");
  const [loadedAssign, setLoadedAssign] = useState(false);

  async function loadCatalog() {
    try { setCatalog(await apiGet("/api/roles")); setCatErr(""); }
    catch (e) { setCatErr(`تعذّر التحميل: ${e.message}`); }
  }
  useEffect(() => { loadCatalog(); }, []);

  async function loadEmps() {
    const params = new URLSearchParams({ page: "1", pageSize: "50" });
    if (aSearch) params.set("search", aSearch);
    try { const d = await apiGet(`/api/employees?${params}`); setEmps(d.items); setLoadedAssign(true); }
    catch (e) { toast(`تعذّر تحميل الموظفين: ${e.message}`); }
  }
  useEffect(() => { if (tab === "asg") { const t = setTimeout(loadEmps, 200); return () => clearTimeout(t); } }, [tab, aSearch]);

  async function addRole(e) {
    e.preventDefault();
    if (!rName.trim()) { setCatErr("اسم الدور مطلوب."); return; }
    try {
      const d = await apiSend("/api/roles", "POST", { name: rName.trim(), description: rDesc.trim() || null });
      setRName(""); setRDesc(""); await loadCatalog(); toast(`أُضيف الدور "${d.name}".`);
    } catch (e) { setCatErr(e.message); }
  }
  async function saveDesc(r, desc) {
    try { await apiSend(`/api/roles/${encodeURIComponent(r.name)}`, "PUT", { name: r.name, description: desc || null }); toast(`حُفظ "${r.name}".`); }
    catch (e) { toast(`فشل الحفظ: ${e.message}`); }
  }
  async function delRole(r) {
    if (!confirm(`حذف الدور "${r.name}"؟ سيُزال أيضاً من كل موظف معيَّن له.`)) return;
    try { await fetch(`/api/roles/${encodeURIComponent(r.name)}`, { method: "DELETE" }); await loadCatalog(); toast(`حُذف "${r.name}".`); }
    catch (e) { toast(`فشل الحذف: ${e.message}`); }
  }
  async function saveRoles(emp, roleNames) {
    try {
      await apiSend(`/api/employees/${encodeURIComponent(emp.employeeId)}/roles`, "PUT", { roles: roleNames });
      toast(`حُفظ ${emp.employeeId} — ${roleNames.length} دور.`);
      loadCatalog();
    } catch (e) { toast(`فشل الحفظ: ${e.message}`); }
  }

  return (
    <React.Fragment>
      <PageHead title="الصلاحيات والأدوار" desc="عرّف الأدوار في الكتالوج، ثم عيّنها للموظفين للتحكّم في صلاحيات الوصول." />
      <div className="card">
        <div className="tabs">
          <button className={"tab" + (tab === "cat" ? " active" : "")} onClick={() => setTab("cat")}>كتالوج الأدوار</button>
          <button className={"tab" + (tab === "asg" ? " active" : "")} onClick={() => setTab("asg")}>تعيين الموظفين</button>
        </div>

        {tab === "cat" ? (
          <div className="fade-in">
            <form className="formrow" style={{ marginBottom: 20 }} onSubmit={addRole}>
              <div className="field"><label>اسم الدور</label><input value={rName} onChange={(e) => setRName(e.target.value)} placeholder="مثال: مشرف" /></div>
              <div className="field" style={{ flex: 2 }}><label>الوصف (اختياري)</label><input value={rDesc} onChange={(e) => setRDesc(e.target.value)} placeholder="وصف موجز لصلاحيات الدور" /></div>
              <button className="btn btn-primary" type="submit"><i className="ti ti-plus"></i>إضافة دور</button>
            </form>
            {catErr && <p className="error" style={{ color: "var(--color-danger-dark)", margin: "-8px 0 14px" }}>{catErr}</p>}
            <div className="table-wrap">
              <table className="data">
                <thead><tr><th>الاسم</th><th>الوصف</th><th>عدد المعيَّنين</th><th></th></tr></thead>
                <tbody>
                  {catalog.length === 0 ? (
                    <tr><td colSpan={4} className="empty-row">لا توجد أدوار بعد — أضف دوراً أعلاه.</td></tr>
                  ) : catalog.map((r) => <CatalogRow key={r.name} r={r} onSave={saveDesc} onDelete={delRole} />)}
                </tbody>
              </table>
            </div>
          </div>
        ) : (
          <div className="fade-in">
            <div className="formrow" style={{ marginBottom: 14 }}>
              <div className="field" style={{ maxWidth: 320 }}>
                <label>بحث</label>
                <div className="in-icon"><i className="ti ti-search"></i><input value={aSearch} onChange={(e) => setASearch(e.target.value)} placeholder="الرقم الوظيفي أو الاسم" /></div>
              </div>
            </div>
            <div className="table-wrap">
              <table className="data">
                <thead><tr><th>الرقم الوظيفي</th><th>الاسم</th><th>الأدوار (اضغط للتبديل ثم احفظ)</th><th></th></tr></thead>
                <tbody>
                  {!loadedAssign ? (
                    <tr><td colSpan={4} className="empty-row">جارٍ التحميل…</td></tr>
                  ) : catalog.length === 0 ? (
                    <tr><td colSpan={4} className="empty-row">عرّف دوراً واحداً على الأقل في كتالوج الأدوار أولاً.</td></tr>
                  ) : emps.length === 0 ? (
                    <tr><td colSpan={4} className="empty-row">لا يوجد موظفون.</td></tr>
                  ) : emps.map((emp) => <AssignRow key={emp.employeeId} emp={emp} catalog={catalog} onSave={saveRoles} />)}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>
    </React.Fragment>
  );
}

function CatalogRow({ r, onSave, onDelete }) {
  const [desc, setDesc] = useState(r.description || "");
  return (
    <tr>
      <td><span className="chip chip-primary" style={{ padding: "4px 11px" }}><span className="dot"></span>{r.name}</span></td>
      <td style={{ whiteSpace: "normal" }}><input className="field" style={{ width: "100%", padding: "7px 10px", border: "1px solid var(--color-border)", borderRadius: "var(--radius-md)", fontFamily: "var(--font-ar)", fontSize: 13 }} value={desc} maxLength={256} onChange={(e) => setDesc(e.target.value)} /></td>
      <td><span className="lat" style={{ fontWeight: 700 }}>{r.assignedTo != null ? r.assignedTo : 0}</span> موظف</td>
      <td>
        <div style={{ display: "flex", gap: 6 }}>
          <button className="btn btn-soft btn-sm" onClick={() => onSave(r, desc.trim())}><i className="ti ti-device-floppy"></i></button>
          <button className="btn btn-danger btn-sm" onClick={() => onDelete(r)}><i className="ti ti-trash"></i></button>
        </div>
      </td>
    </tr>
  );
}

function AssignRow({ emp, catalog, onSave }) {
  const [sel, setSel] = useState(() => new Set(emp.roles || []));
  function toggle(name) { setSel((s) => { const n = new Set(s); n.has(name) ? n.delete(name) : n.add(name); return n; }); }
  return (
    <tr>
      <td><span className="id-badge">{emp.employeeId}</span></td>
      <td><div className="emp-cell"><div className="av" style={{ background: avColor(emp.employeeId) }}>{firstLetter(emp.displayName)}</div><span dir="auto">{emp.displayName || "—"}</span></div></td>
      <td>
        <div className="roles-wrap" style={{ justifyContent: "flex-start" }}>
          {catalog.map((r) => (
            <button key={r.name} className="tag" style={{ border: 0, opacity: sel.has(r.name) ? 1 : 0.45 }} onClick={() => toggle(r.name)}>
              {r.name}<i className={"ti " + (sel.has(r.name) ? "ti-check" : "ti-plus")} style={{ fontSize: 13 }}></i>
            </button>
          ))}
        </div>
      </td>
      <td><button className="btn btn-soft btn-sm" onClick={() => onSave(emp, [...sel])}><i className="ti ti-device-floppy"></i>حفظ</button></td>
    </tr>
  );
}

Object.assign(window, { RosterScreen, UploadScreen, EmployeesScreen, RolesScreen });
