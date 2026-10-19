/* ============================================================ DAWAM — Leaves
   Apply for leave + leave approvals, ported from the Dawam mobile app's leave
   flow. Everything goes through /api/leave-requests, which calls the same
   SmartHCM operations as the app (GetLeaveTypes, GET_TO_DATE,
   Leave_getSupervisorName, SaveLeave, Hrs_GetPendingLeaves, Hrs_GetLeaveDetail,
   Hrs_Leave_PerformAction) as the signed-in user. The browser never holds the
   SmartHCM password — the server keeps it for the session from the login.
     • LeaveApplyScreen     — request form + "my requests" (withdraw while pending)
     • LeaveApprovalsScreen — pending requests from others: approve / forward /
                              reject / cancel, with remarks */
const { useState: useStateL, useEffect: useEffectL, useRef: useRefL } = React;

const LEAVE_API = "/api/leave-requests";
const SESSION_LOST = "انتهت جلسة SmartHCM — يرجى تسجيل الخروج ثم الدخول مجدداً.";

function leaveErr(e, fallback) {
  if (e && e.status === 401) return SESSION_LOST;
  return (e && e.message) || fallback;
}

function todayIso() {
  const n = new Date();
  return `${n.getFullYear()}-${String(n.getMonth() + 1).padStart(2, "0")}-${String(n.getDate()).padStart(2, "0")}`;
}

function LeaveStatus({ r }) {
  const id = (r.statusId || "").toUpperCase();
  const map = {
    P: ["chip-warn", "قيد الانتظار"],
    S: ["chip-warn", "قيد الانتظار"],
    A: ["chip-success", "معتمدة"],
    R: ["chip-danger", "مرفوضة"],
    C: ["chip-muted", "مسحوبة"],
  };
  const [cls, label] = map[id] || ["chip-muted", r.status || "—"];
  return <span className={"chip " + cls} title={r.status || ""}><span className="dot"></span>{label}</span>;
}

function LeaveDays({ r }) {
  if (r.dayHourRequested == null) return <span className="muted">—</span>;
  const n = Number(r.dayHourRequested);
  const hours = (r.dayType || "").toUpperCase() === "H";
  const unit = hours ? (n === 1 ? "ساعة" : "ساعات") : (n === 1 ? "يوم" : "أيام");
  return <span><span className="lat">{r.dayHourRequested}</span> {unit}</span>;
}

function LeaveRange({ r }) {
  if (!r.fromDate) return <span className="muted">—</span>;
  return (
    <span className="lat" dir="ltr">
      {r.fromDate}{r.toDate && r.toDate !== r.fromDate ? ` → ${r.toDate}` : ""}
    </span>
  );
}

/* Remarks-like columns from Hrs_GetLeaveDetail (column names vary by build). */
function detailRemarks(d) {
  if (!d) return [];
  return Object.keys(d)
    .filter((k) => /remark|note|comment|reason/i.test(k) && String(d[k] || "").trim())
    .map((k) => ({ k, v: String(d[k]).trim() }));
}

/* ---------------------------------------------------------------- Apply */
function LeaveApplyScreen() {
  const fileRef = useRefL(null);
  const [types, setTypes] = useStateL([]);
  const [typesErr, setTypesErr] = useStateL("");

  const [leaveType, setLeaveType] = useStateL("");
  const [from, setFrom] = useStateL(todayIso());
  const [to, setTo] = useStateL(todayIso());
  const [hourly, setHourly] = useStateL(false);
  const [fromTime, setFromTime] = useStateL("");
  const [toTime, setToTime] = useStateL("");
  const [days, setDays] = useStateL("");
  const [daysBusy, setDaysBusy] = useStateL(false);
  const [supervisor, setSupervisor] = useStateL(null);
  const [remarks, setRemarks] = useStateL("");
  const [files, setFiles] = useStateL([]);
  const [busy, setBusy] = useStateL(false);
  const [formErr, setFormErr] = useStateL("");
  const [result, setResult] = useStateL(null);

  const [mine, setMine] = useStateL(null);
  const [mineErr, setMineErr] = useStateL("");
  const [actionBusy, setActionBusy] = useStateL("");

  async function loadTypes() {
    setTypesErr("");
    try { setTypes(await apiGet(`${LEAVE_API}/types`)); }
    catch (e) { setTypesErr(leaveErr(e, "تعذّر جلب أنواع الإجازات.")); }
  }
  async function loadMine() {
    setMineErr("");
    try { setMine(await apiGet(`${LEAVE_API}/mine`)); }
    catch (e) { setMineErr(leaveErr(e, "تعذّر جلب طلباتك.")); setMine([]); }
  }
  useEffectL(() => { loadTypes(); loadMine(); }, []);

  // Ask SmartHCM for the effective day count (weekends / holidays aware),
  // then who will approve a leave of that type and length.
  useEffectL(() => {
    setSupervisor(null);
    if (!leaveType || !from || !to || to < from) { setDays(""); return; }
    if (hourly && (!fromTime || !toTime)) return;
    let cancelled = false;
    const t = setTimeout(async () => {
      setDaysBusy(true);
      try {
        const q = new URLSearchParams({ leaveType, from, to });
        if (hourly) { q.set("fromTime", fromTime); q.set("toTime", toTime); }
        const d = await apiGet(`${LEAVE_API}/days?${q}`);
        if (cancelled) return;
        const n = d && d.days != null ? d.days : "";
        setDays(n === "" ? "" : String(n));
        const s = await apiGet(`${LEAVE_API}/supervisor?${new URLSearchParams({ leaveType, days: String(n || 1) })}`);
        if (!cancelled) setSupervisor(s);
      } catch (e) {
        if (!cancelled) setFormErr(leaveErr(e, "تعذّر احتساب عدد الأيام."));
      } finally {
        if (!cancelled) setDaysBusy(false);
      }
    }, 300);
    return () => { cancelled = true; clearTimeout(t); };
  }, [leaveType, from, to, hourly, fromTime, toTime]);

  function pickFiles(list) {
    const arr = Array.from(list || []);
    const tooBig = arr.find((f) => f.size > 10 * 1024 * 1024);
    if (tooBig) { setFormErr(`الملف «${tooBig.name}» أكبر من ١٠ ميجابايت.`); return; }
    setFiles((cur) => [...cur, ...arr].slice(0, 5));
    if (fileRef.current) fileRef.current.value = "";
  }

  async function submit(e) {
    e.preventDefault();
    setFormErr(""); setResult(null);
    if (!leaveType) { setFormErr("اختر نوع الإجازة."); return; }
    if (!from || !to || to < from) { setFormErr("تاريخ النهاية يجب أن يكون في تاريخ البداية أو بعده."); return; }
    if (hourly && (!fromTime || !toTime)) { setFormErr("أدخل وقت البداية والنهاية للإجازة بالساعات."); return; }
    const n = Number(days);
    if (!(n > 0)) { setFormErr("عدد الأيام يجب أن يكون أكبر من صفر."); return; }

    setBusy(true);
    try {
      const form = new FormData();
      form.append("leaveType", leaveType);
      form.append("fromDate", from);
      form.append("toDate", to);
      form.append("noOfDays", String(n));
      form.append("remarks", remarks.trim());
      if (hourly) { form.append("fromTime", fromTime); form.append("toTime", toTime); }
      files.forEach((f) => form.append("files", f, f.name));
      const r = await api(LEAVE_API, { method: "POST", body: form });
      setResult(r);
      if (r && r.success) {
        toast("تم تقديم طلب الإجازة.");
        setRemarks(""); setFiles([]);
        loadMine();
      }
    } catch (e2) {
      setFormErr(leaveErr(e2, "تعذّر تقديم الطلب."));
    } finally {
      setBusy(false);
    }
  }

  async function withdraw(r) {
    if (!confirm(`سحب طلب الإجازة (${r.leaveName || r.leaveType || ""} ${r.fromDate || ""})؟`)) return;
    setActionBusy(r.rflNo);
    try {
      const res = await apiSend(`${LEAVE_API}/${encodeURIComponent(r.rflNo)}/action`, "POST", { action: "C", remarks: "" });
      if (res && res.success) { toast("تم سحب الطلب."); loadMine(); }
      else toast((res && res.message) || "رفض SmartHCM سحب الطلب.");
    } catch (e) {
      toast(leaveErr(e, "تعذّر سحب الطلب."));
    } finally {
      setActionBusy("");
    }
  }

  const selectedType = types.find((t) => t.id === leaveType);
  const pendingCount = (mine || []).filter((r) => r.isPending).length;

  return (
    <React.Fragment>
      <PageHead title="طلب إجازة" desc="قدّم طلب إجازة إلى SmartHCM وتابع حالة طلباتك — بنفس طريقة تطبيق دوام. يُحتسب عدد الأيام تلقائياً مع مراعاة العطل." />

      <div className="card">
        <div className="card-head"><h2>طلب جديد</h2></div>
        {typesErr && <p className="error" style={{ color: "var(--color-danger-dark)", margin: "0 0 12px" }}>{typesErr}</p>}
        <form onSubmit={submit}>
          <div className="formrow" style={{ marginBottom: 14 }}>
            <div className="field" style={{ minWidth: 240 }}>
              <label>نوع الإجازة</label>
              <select value={leaveType} onChange={(e) => setLeaveType(e.target.value)}>
                <option value="">— اختر —</option>
                {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
              </select>
            </div>
            <div className="field"><label>من تاريخ</label><input type="date" value={from} onChange={(e) => { setFrom(e.target.value); if (to < e.target.value) setTo(e.target.value); }} /></div>
            <div className="field"><label>إلى تاريخ</label><input type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} /></div>
            <div className="field" style={{ maxWidth: 150 }}>
              <label>عدد الأيام {daysBusy && <i className="ti ti-loader-2" style={{ animation: "spin .7s linear infinite" }}></i>}</label>
              <input className="lat" inputMode="decimal" value={days} onChange={(e) => setDays(e.target.value)} placeholder="—" />
            </div>
          </div>

          <div className="formrow" style={{ marginBottom: 14 }}>
            <label style={{ display: "inline-flex", alignItems: "center", gap: 6, fontSize: 13, cursor: "pointer", alignSelf: "center" }}>
              <input type="checkbox" checked={hourly} onChange={(e) => setHourly(e.target.checked)} />
              إجازة بالساعات
            </label>
            {hourly && <div className="field" style={{ maxWidth: 160 }}><label>من الساعة</label><input type="time" value={fromTime} onChange={(e) => setFromTime(e.target.value)} /></div>}
            {hourly && <div className="field" style={{ maxWidth: 160 }}><label>إلى الساعة</label><input type="time" value={toTime} onChange={(e) => setToTime(e.target.value)} /></div>}
          </div>

          {selectedType && (selectedType.maxStretch || selectedType.minRequiredLimit) && (
            <p className="muted small" style={{ margin: "0 0 12px" }}>
              {selectedType.maxStretch && <span>أقصى مدة متصلة: <span className="lat">{selectedType.maxStretch}</span> · </span>}
              {selectedType.minRequiredLimit && <span>الحد الأدنى: <span className="lat">{selectedType.minRequiredLimit}</span></span>}
            </p>
          )}

          {supervisor && (supervisor.name || supervisor.id) && (
            <p style={{ margin: "0 0 14px", fontSize: 13 }}>
              <i className="ti ti-user-check" style={{ verticalAlign: "-2px", marginInlineStart: 4 }}></i>
              {" "}سيعتمد الطلب: <b dir="auto">{supervisor.name || "—"}</b>{supervisor.id && <span className="lat muted"> ({supervisor.id})</span>}
            </p>
          )}

          <div className="field" style={{ marginBottom: 14 }}>
            <label>ملاحظات (اختياري)</label>
            <textarea rows={3} value={remarks} onChange={(e) => setRemarks(e.target.value)} maxLength={500}
              style={{ width: "100%", padding: "9px 12px", border: "1px solid var(--color-border)", borderRadius: "var(--radius-md)", fontFamily: "var(--font-ar)", fontSize: 13.5, resize: "vertical" }} />
          </div>

          <div className="field" style={{ marginBottom: 16 }}>
            <label>المرفقات (تقرير طبي أو غيره — حتى ٥ ملفات، ١٠ ميجابايت لكل ملف)</label>
            <input ref={fileRef} type="file" multiple accept=".pdf,.jpg,.jpeg,.png,.doc,.docx" style={{ display: "none" }}
              onChange={(e) => pickFiles(e.target.files)} />
            <div style={{ display: "flex", gap: 8, flexWrap: "wrap", alignItems: "center" }}>
              <button type="button" className="btn btn-soft btn-sm" onClick={() => fileRef.current && fileRef.current.click()} disabled={files.length >= 5}>
                <i className="ti ti-paperclip"></i>إرفاق ملف
              </button>
              {files.map((f, i) => (
                <span key={i} className="tag">
                  <span className="lat">{f.name}</span>
                  <i className="ti ti-x" style={{ cursor: "pointer", fontSize: 13 }} onClick={() => setFiles((cur) => cur.filter((_, j) => j !== i))}></i>
                </span>
              ))}
            </div>
            {files.length > 0 && <p className="muted small" style={{ margin: "6px 0 0" }}>سيُسجَّل الطلب بأنه مرفق به تقرير طبي.</p>}
          </div>

          {formErr && <p className="error" style={{ color: "var(--color-danger-dark)", margin: "0 0 12px" }}>{formErr}</p>}
          {result && (
            <p style={{ margin: "0 0 12px", color: result.success ? "var(--color-success-dark, #2e7d32)" : "var(--color-danger-dark)" }}>
              <i className={"ti " + (result.success ? "ti-circle-check" : "ti-alert-circle")} style={{ verticalAlign: "-2px", marginInlineStart: 4 }}></i>
              {" "}{result.success ? "تم تقديم الطلب بنجاح." : "رفض SmartHCM الطلب:"} <span dir="auto">{result.message}</span>
            </p>
          )}

          <button className="btn btn-primary" type="submit" disabled={busy || daysBusy}>
            <i className={"ti " + (busy ? "ti-loader-2" : "ti-send")} style={busy ? { animation: "spin .7s linear infinite" } : null}></i>
            {busy ? "جارٍ الإرسال..." : "تقديم الطلب"}
          </button>
        </form>
      </div>

      <div className="card">
        <div className="card-head">
          <h2>طلباتي</h2>
          <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
            {pendingCount > 0 && <span className="chip chip-warn"><span className="dot"></span><span className="lat">{pendingCount}</span> قيد الانتظار</span>}
            <button className="btn btn-ghost btn-sm" onClick={loadMine}><i className="ti ti-refresh"></i>تحديث</button>
          </div>
        </div>
        {mineErr && <p className="error" style={{ color: "var(--color-danger-dark)", margin: "0 0 12px" }}>{mineErr}</p>}
        <div className="table-wrap">
          <table className="data">
            <thead><tr><th>رقم الطلب</th><th>نوع الإجازة</th><th>الفترة</th><th>المدة</th><th>الحالة</th><th></th></tr></thead>
            <tbody>
              {mine == null ? (
                <tr><td colSpan={6} className="empty-row">جارٍ التحميل…</td></tr>
              ) : mine.length === 0 ? (
                <tr><td colSpan={6} className="empty-row">لا توجد طلبات إجازة.</td></tr>
              ) : mine.map((r, i) => (
                <tr key={r.rflNo || i}>
                  <td>{r.rflNo ? <span className="id-badge">{r.rflNo}</span> : <span className="muted">—</span>}</td>
                  <td dir="auto">{r.leaveName || r.leaveType || "—"}</td>
                  <td><LeaveRange r={r} /></td>
                  <td><LeaveDays r={r} /></td>
                  <td><LeaveStatus r={r} /></td>
                  <td>
                    {r.isPending && r.rflNo && (
                      <button className="btn btn-danger btn-sm" disabled={actionBusy === r.rflNo} onClick={() => withdraw(r)} title="سحب الطلب">
                        <i className={"ti " + (actionBusy === r.rflNo ? "ti-loader-2" : "ti-arrow-back-up")}></i>سحب
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </React.Fragment>
  );
}

/* ---------------------------------------------------------------- Approvals */
function LeaveApprovalsScreen({ onCount }) {
  const [rows, setRows] = useStateL(null);
  const [err, setErr] = useStateL("");
  const [loading, setLoading] = useStateL(false);
  const [details, setDetails] = useStateL({});      // rflNo -> detail row | "loading"
  const [pending, setPending] = useStateL(null);    // { row, action }
  const [remarks, setRemarks] = useStateL("");
  const [busy, setBusy] = useStateL(false);

  async function load() {
    setLoading(true); setErr("");
    try {
      const d = await apiGet(`${LEAVE_API}/approvals`);
      setRows(d);
      if (onCount) onCount(d.length);
    } catch (e) {
      setErr(leaveErr(e, "تعذّر جلب طلبات الاعتماد.")); setRows([]);
    } finally {
      setLoading(false);
    }
  }
  useEffectL(() => { load(); }, []);

  async function toggleDetail(r) {
    if (!r.rflNo) return;
    if (details[r.rflNo]) { setDetails((d) => { const n = { ...d }; delete n[r.rflNo]; return n; }); return; }
    setDetails((d) => ({ ...d, [r.rflNo]: "loading" }));
    try {
      const d = await apiGet(`${LEAVE_API}/${encodeURIComponent(r.rflNo)}?applicantId=${encodeURIComponent(r.employeeId || "")}`);
      setDetails((cur) => ({ ...cur, [r.rflNo]: d || {} }));
    } catch (e) {
      setDetails((cur) => ({ ...cur, [r.rflNo]: { __error: leaveErr(e, "تعذّر جلب التفاصيل.") } }));
    }
  }

  function start(row, action) { setPending({ row, action }); setRemarks(""); window.scrollTo(0, 0); }

  async function confirmAction() {
    if (!pending) return;
    const { row, action } = pending;
    if (action === "R" && !remarks.trim()) { toast("اكتب سبب الرفض."); return; }
    setBusy(true);
    try {
      const res = await apiSend(`${LEAVE_API}/${encodeURIComponent(row.rflNo)}/action`, "POST", { action, remarks: remarks.trim() });
      if (res && res.success) {
        toast(action === "A" ? (row.requiresHrApproval ? "تم تحويل الطلب للموارد البشرية." : "تم اعتماد الطلب.")
          : action === "R" ? "تم رفض الطلب." : "تم إلغاء الطلب.");
        setPending(null);
        const left = (rows || []).filter((r) => r.rflNo !== row.rflNo);
        setRows(left);
        if (onCount) onCount(left.length);
      } else {
        toast((res && res.message) || "رفض SmartHCM الإجراء.");
      }
    } catch (e) {
      toast(leaveErr(e, "تعذّر تنفيذ الإجراء."));
    } finally {
      setBusy(false);
    }
  }

  const actionLabel = (p) =>
    p.action === "A" ? (p.row.requiresHrApproval ? "تحويل للموارد البشرية" : "اعتماد")
      : p.action === "R" ? "رفض" : "إلغاء";

  return (
    <React.Fragment>
      <PageHead title="اعتماد الإجازات" desc="طلبات الإجازة المعلّقة لموظفيك في SmartHCM — اعتمدها أو ارفضها. بعض الأنواع تُحوَّل للموارد البشرية بعد موافقتك.">
        <button className="btn btn-ghost btn-sm" onClick={load} disabled={loading}>
          <i className={"ti " + (loading ? "ti-loader-2" : "ti-refresh")} style={loading ? { animation: "spin .7s linear infinite" } : null}></i>تحديث
        </button>
      </PageHead>

      {pending && (
        <div className="card" style={{ borderInlineStart: "4px solid var(--color-primary)" }}>
          <div className="card-head">
            <h2>{actionLabel(pending)} — <span dir="auto">{pending.row.employeeName || pending.row.employeeId}</span></h2>
          </div>
          <p style={{ margin: "0 0 12px", fontSize: 13 }}>
            <span dir="auto">{pending.row.leaveName || pending.row.leaveType}</span> · <LeaveRange r={pending.row} /> · <LeaveDays r={pending.row} />
          </p>
          <div className="field" style={{ marginBottom: 14 }}>
            <label>{pending.action === "R" ? "سبب الرفض (مطلوب)" : "ملاحظات (اختياري)"}</label>
            <textarea rows={3} value={remarks} onChange={(e) => setRemarks(e.target.value)} maxLength={500}
              style={{ width: "100%", padding: "9px 12px", border: "1px solid var(--color-border)", borderRadius: "var(--radius-md)", fontFamily: "var(--font-ar)", fontSize: 13.5, resize: "vertical" }} />
          </div>
          <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
            <button className={"btn " + (pending.action === "A" ? "btn-primary" : "btn-danger")} disabled={busy} onClick={confirmAction}>
              <i className={"ti " + (busy ? "ti-loader-2" : "ti-check")} style={busy ? { animation: "spin .7s linear infinite" } : null}></i>تأكيد {actionLabel(pending)}
            </button>
            <button className="btn btn-ghost" disabled={busy} onClick={() => setPending(null)}>تراجع</button>
          </div>
        </div>
      )}

      <div className="card">
        <div className="card-head">
          <h2>طلبات بانتظار اعتمادك</h2>
          {rows && <span className="chip chip-primary"><span className="dot"></span><span className="lat">{rows.length}</span> طلب</span>}
        </div>
        {err && <p className="error" style={{ color: "var(--color-danger-dark)", margin: "0 0 12px" }}>{err}</p>}
        <div className="table-wrap">
          <table className="data">
            <thead><tr><th>الموظف</th><th>نوع الإجازة</th><th>الفترة</th><th>المدة</th><th>الحالة</th><th>التفاصيل</th><th>الإجراء</th></tr></thead>
            <tbody>
              {rows == null ? (
                <tr><td colSpan={7} className="empty-row">جارٍ التحميل…</td></tr>
              ) : rows.length === 0 ? (
                <tr><td colSpan={7} className="empty-row">لا توجد طلبات بانتظار اعتمادك.</td></tr>
              ) : rows.map((r, i) => {
                const d = r.rflNo ? details[r.rflNo] : null;
                const notes = d && d !== "loading" && !d.__error ? detailRemarks(d) : [];
                return (
                  <React.Fragment key={r.rflNo || i}>
                    <tr>
                      <td>
                        <div className="emp-cell">
                          <div className="av" style={{ background: avColor(r.employeeId) }}>{firstLetter(r.employeeName)}</div>
                          <div><div dir="auto">{r.employeeName || "—"}</div><div className="lat muted small">{r.employeeId}</div></div>
                        </div>
                      </td>
                      <td dir="auto">
                        {r.leaveName || r.leaveType || "—"}
                        {r.requiresHrApproval && <div><span className="pill pill-muted" title="موافقتك تحوّل الطلب للموارد البشرية للاعتماد النهائي">يُحوَّل للموارد البشرية</span></div>}
                      </td>
                      <td style={{ whiteSpace: "normal", minWidth: 110 }}><LeaveRange r={r} /></td>
                      <td><LeaveDays r={r} /></td>
                      <td><LeaveStatus r={r} /></td>
                      <td>
                        {r.rflNo && (
                          <button className="btn btn-ghost btn-sm" onClick={() => toggleDetail(r)}>
                            <i className={"ti " + (d === "loading" ? "ti-loader-2" : d ? "ti-chevron-up" : "ti-file-description")}></i>
                            <span className="lat">{r.rflNo}</span>
                          </button>
                        )}
                      </td>
                      <td>
                        {r.rflNo ? (
                          <div style={{ display: "flex", gap: 6, flexWrap: "nowrap" }}>
                            <button className="btn btn-primary btn-sm" onClick={() => start(r, "A")}>
                              <i className={"ti " + (r.requiresHrApproval ? "ti-arrow-forward-up" : "ti-check")}></i>{r.requiresHrApproval ? "تحويل" : "اعتماد"}
                            </button>
                            <button className="btn btn-danger btn-sm" onClick={() => start(r, "R")}><i className="ti ti-x"></i>رفض</button>
                            {!r.requiresHrApproval && (
                              <button className="btn btn-ghost btn-sm" onClick={() => start(r, "C")} title="إلغاء الطلب" aria-label="إلغاء الطلب"><i className="ti ti-ban"></i></button>
                            )}
                          </div>
                        ) : <span className="muted small">لا يوجد رقم طلب</span>}
                      </td>
                    </tr>
                    {d && d !== "loading" && (
                      <tr>
                        <td colSpan={7} style={{ background: "var(--color-bg)", whiteSpace: "normal" }}>
                          {d.__error ? <span style={{ color: "var(--color-danger-dark)" }}>{d.__error}</span>
                            : notes.length ? notes.map((n) => <div key={n.k}><span className="muted small lat">{n.k}:</span> <span dir="auto">{n.v}</span></div>)
                            : <span className="muted">لا توجد ملاحظات من الموظف.</span>}
                        </td>
                      </tr>
                    )}
                  </React.Fragment>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </React.Fragment>
  );
}

Object.assign(window, { LeaveApplyScreen, LeaveApprovalsScreen });
