/* ============================================================ DAWAM — Daily Absent Report
   One row per employee / day / period-group with no attendance (status not
   P/L). Data comes from /api/reports/daily-absent (executed server-side via
   the HR API where the SmartHCM database is reachable). The "تسوية" action
   records an attendance adjustment through the HR API, which enforces the
   signed-in user's adjust-attendance rights. */
const { useState: useStateA, useEffect: useEffectA } = React;

function absGroupLabel(key) {
  if (key === "GRP_MORNING") return "صباحية";
  if (key === "GRP_EVENING") return "مسائية";
  return key; // P_7, P_8, …
}

function AbsentRow({ r }) {
  const [state, setState] = useStateA("idle"); // idle | busy | done

  async function adjust() {
    const notes = window.prompt(
      `تسوية حضور:\n${r.employeeName || r.employeeId} — ${r.attendDate} (${absGroupLabel(r.groupKey)})\n\nملاحظة (اختياري):`, "");
    if (notes === null) return; // cancelled
    setState("busy");
    try {
      await apiSend("/api/reports/daily-absent/adjust", "POST", {
        employeeId: r.employeeId,
        attendanceDate: r.attendDate,
        notes: notes || null,
      });
      toast(`تمت تسوية الحضور لـ ${r.employeeId} بتاريخ ${r.attendDate}`);
      setState("done");
    } catch (e) {
      toast(`فشلت التسوية: ${e.message}`);
      setState("idle");
    }
  }

  const groupCls = r.groupKey === "GRP_MORNING" ? "chip chip-primary"
                 : r.groupKey === "GRP_EVENING" ? "chip chip-warn"
                 : "chip";
  return (
    <tr>
      <td className="lat">{r.attendDate}</td>
      <td><span className="id-badge">{r.employeeId}</span></td>
      <td><div className="emp-cell"><div className="av" style={{ background: avColor(r.employeeId) }}>{firstLetter(r.employeeName)}</div><span dir="auto">{r.employeeName || "—"}</span></div></td>
      <td dir="auto">{r.position || <span className="muted">—</span>}</td>
      <td dir="auto">{r.department || <span className="muted">—</span>}</td>
      <td><span className={groupCls}>{absGroupLabel(r.groupKey)}</span></td>
      <td className="lat">{r.periods || "—"}</td>
      <td className="lat">{r.requiredHours}</td>
      <td className="lat">{r.presentHours}</td>
      <td className="lat">{r.leaveHours}</td>
      <td className="lat">{r.attendanceStatus || "—"}</td>
      <td>
        {state === "done" ? (
          <span className="chip chip-success"><i className="ti ti-check"></i> تمت التسوية</span>
        ) : (
          <button className="btn btn-soft btn-sm" disabled={state === "busy"}
            title="تسجيل تسوية حضور لهذا اليوم" onClick={adjust}>
            <i className={"ti " + (state === "busy" ? "ti-loader-2" : "ti-clock-edit")}
               style={state === "busy" ? { animation: "spin .7s linear infinite" } : null}></i>
            تسوية
          </button>
        )}
      </td>
    </tr>
  );
}

function AbsentReportScreen() {
  const today = (() => {
    const n = new Date();
    return `${n.getFullYear()}-${String(n.getMonth() + 1).padStart(2, "0")}-${String(n.getDate()).padStart(2, "0")}`;
  })();
  const [from, setFrom] = useStateA(today);
  const [to, setTo] = useStateA(today);
  const [empId, setEmpId] = useStateA("");
  const [posFilter, setPosFilter] = useStateA("");   // المنصب
  const [groupFilter, setGroupFilter] = useStateA(""); // المجموعة (الفترة)
  const [rows, setRows] = useStateA(null);
  const [error, setError] = useStateA("");
  const [loading, setLoading] = useStateA(false);

  async function loadReport() {
    if (!from || !to) { setError("اختر التاريخين."); return; }
    setLoading(true); setError(""); setRows(null);
    // Reset the client-side filters — the previous selection may not exist in
    // the newly fetched range.
    setPosFilter(""); setGroupFilter("");
    try {
      const url = `/api/reports/daily-absent?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`
                + (empId.trim() ? `&employeeId=${encodeURIComponent(empId.trim())}` : "");
      const data = await apiGet(url);
      setRows(Array.isArray(data) ? data : []);
    } catch (e) {
      setError(e.message || "تعذّر جلب التقرير.");
    } finally {
      setLoading(false);
    }
  }

  // Auto-load today's absences on first open.
  useEffectA(() => { loadReport(); }, []);

  // Distinct positions / period-groups present in the loaded rows, for the
  // dropdowns. Groups are ordered morning → evening → the rest.
  const positions = rows
    ? Array.from(new Set(rows.map((r) => r.position).filter(Boolean))).sort((a, b) => a.localeCompare(b, "ar"))
    : [];
  const GROUP_ORDER = { GRP_MORNING: 0, GRP_EVENING: 1 };
  const groups = rows
    ? Array.from(new Set(rows.map((r) => r.groupKey).filter(Boolean)))
        .sort((a, b) => (GROUP_ORDER[a] ?? 9) - (GROUP_ORDER[b] ?? 9) || a.localeCompare(b))
    : [];

  const filtered = (rows || []).filter((r) =>
    (!posFilter || r.position === posFilter) &&
    (!groupFilter || r.groupKey === groupFilter));

  const empCount = new Set(filtered.map((r) => r.employeeId)).size;

  return (
    <React.Fragment>
      <PageHead title="تقرير الغياب اليومي"
        desc="غياب الهيئة التعليمية والإدارية حسب مجموعات الفترات — صفّ لكل موظف/يوم/مجموعة. المصدر: SmartHCM." />
      <div className="card">
        <div className="card-head"><h2>نطاق التقرير</h2></div>
        <div className="formrow">
          <div className="field"><label>من تاريخ</label><input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></div>
          <div className="field"><label>إلى تاريخ</label><input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></div>
          <div className="field"><label>الرقم الوظيفي (اختياري)</label><input value={empId} onChange={(e) => setEmpId(e.target.value)} className="lat" placeholder="كل الموظفين" /></div>
          <button className="btn btn-primary" disabled={loading} onClick={loadReport}>
            <i className={"ti " + (loading ? "ti-loader-2" : "ti-report-search")}
               style={loading ? { animation: "spin .7s linear infinite" } : null}></i>جلب التقرير
          </button>
        </div>
        {error && <p className="error" style={{ color: "var(--color-danger-dark)", marginTop: 12 }}>{error}</p>}
      </div>

      {rows && rows.length === 0 && (
        <div className="card"><p className="muted" style={{ margin: 0 }}>لا يوجد غياب في النطاق المحدد. 🎉</p></div>
      )}

      {rows && rows.length > 0 && (
        <div className="card">
          <div className="card-head">
            <h2>حالات الغياب</h2>
            <span className="sub">
              <span className="lat">{filtered.length}</span> من <span className="lat">{rows.length}</span> صف ·{" "}
              <span className="lat">{empCount}</span> موظف
            </span>
          </div>

          {/* client-side filters — populated from the loaded rows */}
          <div className="formrow" style={{ marginBottom: 14 }}>
            <div className="field">
              <label>المنصب</label>
              <select value={posFilter} onChange={(e) => setPosFilter(e.target.value)}>
                <option value="">كل المناصب</option>
                {positions.map((p) => <option key={p} value={p}>{p}</option>)}
              </select>
            </div>
            <div className="field">
              <label>المجموعة (الفترة)</label>
              <select value={groupFilter} onChange={(e) => setGroupFilter(e.target.value)}>
                <option value="">كل المجموعات</option>
                {groups.map((g) => <option key={g} value={g}>{absGroupLabel(g)}</option>)}
              </select>
            </div>
            {(posFilter || groupFilter) && (
              <button className="btn btn-soft btn-sm" style={{ alignSelf: "end" }}
                onClick={() => { setPosFilter(""); setGroupFilter(""); }}>
                <i className="ti ti-filter-off"></i>مسح التصفية
              </button>
            )}
          </div>

          {filtered.length === 0 ? (
            <p className="muted" style={{ margin: 0 }}>لا توجد صفوف مطابقة للتصفية المحددة.</p>
          ) : (
          <div className="table-wrap">
            <table className="data">
              <thead>
                <tr>
                  <th>التاريخ</th><th>الرقم الوظيفي</th><th>الاسم</th><th>المنصب</th>
                  <th>القسم</th><th>المجموعة</th><th>الفترات</th>
                  <th>المطلوبة (س)</th><th>الحاضرة (س)</th><th>الإجازة (س)</th>
                  <th>الحالة</th><th></th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((r, i) => (
                  <AbsentRow key={`${r.employeeId}-${r.attendDate}-${r.groupKey}-${i}`}
                    r={r} />
                ))}
              </tbody>
            </table>
          </div>
          )}
        </div>
      )}
    </React.Fragment>
  );
}

Object.assign(window, { AbsentReportScreen });
