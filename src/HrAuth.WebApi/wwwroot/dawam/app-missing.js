/* ============================================================ DAWAM — Missing Punches Report
   Shift halves that recorded exactly ONE punch (unpairable check-in/out).
   Data from /api/reports/missing-punches (executed server-side via the HR
   API). The "تسوية" action records an attendance adjustment through the HR
   API, which enforces the signed-in user's adjust-attendance rights. */
const { useState: useStateP, useEffect: useEffectP } = React;

function mpGroupLabel(key) {
  if ((key || "").indexOf("MORNING") === 0) return "صباحية";
  if ((key || "").indexOf("EVENING") === 0) return "مسائية";
  return key; // P_7, P_8, …
}

function MissingPunchRow({ r }) {
  const [state, setState] = useStateP("idle"); // idle | busy | done

  async function adjust() {
    const notes = window.prompt(
      `تسوية حضور:\n${r.employeeName || r.employeeId} — ${r.attDate} (${mpGroupLabel(r.periodGroup)})\n`
      + `${r.missingType === "Missing Check-In" ? "بصمة الدخول مفقودة" : "بصمة الخروج مفقودة"}\n\nملاحظة (اختياري):`, "");
    if (notes === null) return;
    setState("busy");
    try {
      await apiSend("/api/reports/daily-absent/adjust", "POST", {
        employeeId: r.employeeId,
        attendanceDate: r.attDate,
        notes: notes || null,
      });
      toast(`تمت تسوية الحضور لـ ${r.employeeId} بتاريخ ${r.attDate}`);
      setState("done");
    } catch (e) {
      toast(`فشلت التسوية: ${e.message}`);
      setState("idle");
    }
  }

  const isIn = r.missingType === "Missing Check-In";
  return (
    <tr>
      <td className="lat">{r.attDate}</td>
      <td><span className="id-badge">{r.employeeId}</span></td>
      <td><div className="emp-cell"><div className="av" style={{ background: avColor(r.employeeId) }}>{firstLetter(r.employeeName)}</div><span dir="auto">{r.employeeName || "—"}</span></div></td>
      <td dir="auto">{r.position || <span className="muted">—</span>}</td>
      <td dir="auto">{r.department || <span className="muted">—</span>}</td>
      <td><span className={"chip " + (mpGroupLabel(r.periodGroup) === "صباحية" ? "chip-primary" : "chip-warn")}>{mpGroupLabel(r.periodGroup)}</span></td>
      <td className="lat">{r.periods || "—"}</td>
      <td className="lat">{r.checkIn || "—"}</td>
      <td className="lat">{r.checkOut || "—"}</td>
      <td>
        <span className={"chip " + (isIn ? "chip-danger" : "chip-warn")}>
          {isIn ? "بصمة الدخول مفقودة" : "بصمة الخروج مفقودة"}
        </span>
      </td>
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

function MissingPunchScreen() {
  const today = (() => {
    const n = new Date();
    return `${n.getFullYear()}-${String(n.getMonth() + 1).padStart(2, "0")}-${String(n.getDate()).padStart(2, "0")}`;
  })();
  const [from, setFrom] = useStateP(today);
  const [to, setTo] = useStateP(today);
  const [empId, setEmpId] = useStateP("");
  const [posFilter, setPosFilter] = useStateP("");   // المنصب
  const [groupFilter, setGroupFilter] = useStateP(""); // المجموعة (الفترة)
  const [rows, setRows] = useStateP(null);
  const [error, setError] = useStateP("");
  const [loading, setLoading] = useStateP(false);

  async function loadReport() {
    if (!from || !to) { setError("اختر التاريخين."); return; }
    setLoading(true); setError(""); setRows(null);
    // Reset filters — the prior selection may not exist in the new range.
    setPosFilter(""); setGroupFilter("");
    try {
      const url = `/api/reports/missing-punches?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`
                + (empId.trim() ? `&employeeId=${encodeURIComponent(empId.trim())}` : "");
      const data = await apiGet(url);
      setRows(Array.isArray(data) ? data : []);
    } catch (e) {
      setError(e.message || "تعذّر جلب التقرير.");
    } finally {
      setLoading(false);
    }
  }

  useEffectP(() => { loadReport(); }, []);

  // Distinct positions / period-groups in the loaded rows, for the dropdowns.
  const positions = rows
    ? Array.from(new Set(rows.map((r) => r.position).filter(Boolean))).sort((a, b) => a.localeCompare(b, "ar"))
    : [];
  const groups = rows
    ? Array.from(new Set(rows.map((r) => r.periodGroup).filter(Boolean)))
        .sort((a, b) => a.localeCompare(b))
    : [];

  const filtered = (rows || []).filter((r) =>
    (!posFilter || r.position === posFilter) &&
    (!groupFilter || r.periodGroup === groupFilter));

  const empCount = new Set(filtered.map((r) => r.employeeId)).size;
  const missIn = filtered.filter((r) => r.missingType === "Missing Check-In").length;
  const missOut = filtered.filter((r) => r.missingType === "Missing Check-Out").length;

  return (
    <React.Fragment>
      <PageHead title="تقرير البصمات الناقصة"
        desc="فترات الدوام التي سُجّلت فيها بصمة واحدة فقط — لا يمكن مطابقتها كدخول وخروج. تُستثنى أيام الغياب والإجازة. المصدر: SmartHCM." />
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
        <div className="card"><p className="muted" style={{ margin: 0 }}>لا توجد بصمات ناقصة في النطاق المحدد. 🎉</p></div>
      )}

      {rows && rows.length > 0 && (
        <div className="card">
          <div className="card-head">
            <h2>البصمات الناقصة</h2>
            <span className="sub">
              <span className="lat">{filtered.length}</span> من <span className="lat">{rows.length}</span> صف ·{" "}
              <span className="lat">{empCount}</span> موظف ·
              دخول مفقود <span className="lat">{missIn}</span> · خروج مفقود <span className="lat">{missOut}</span>
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
                {groups.map((g) => <option key={g} value={g}>{mpGroupLabel(g)}</option>)}
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
                  <th>الدخول</th><th>الخروج</th><th>الناقص</th><th></th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((r, i) => (
                  <MissingPunchRow key={`${r.employeeId}-${r.attDate}-${r.periodGroup}-${i}`}
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

Object.assign(window, { MissingPunchScreen });
