/* ============================================================ DAWAM — late / early + calculation rules
   Two screens over the HR API (proxied by HrAuth, bearer token forwarded):
     • LateEarlyScreen       — /api/late-early: one row per employee / day /
       block late or early beyond the grace minutes, paged server-side, with
       totals across the whole range.
     • AttendanceRulesScreen — /api/attendance-rules: every calculation rule
       (scope, period groups, absence, late / early) with its value in force
       and its default; HR admins (SmartHCM Edit on PAY/S017, checked by the HR
       API) change rules from a chosen pay month onward. */
const { useState: useStateR, useEffect: useEffectR } = React;

const MIN = (n) => Number(n || 0).toLocaleString("en-US", { maximumFractionDigits: 2 });
const ruleGroupLabel = (g) => ({ scope: "النطاق", groups: "مجموعات الفترات", absence: "الغياب (D33)", late_early: "التأخر والخروج المبكر" }[g] || g);

/* "2026-09" → calendar month 2026-09-01 .. 2026-09-30 */
function calendarMonthRange(ym) {
  if (!/^\d{4}-\d{2}$/.test(ym || "")) return null;
  const [y, m] = ym.split("-").map(Number);
  const iso = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
  return { from: iso(new Date(y, m - 1, 1)), to: iso(new Date(y, m, 0)) };
}

function lateEarlyCsv(rows) {
  const head = ["record_id", "attend_date", "day", "employee_id", "employee_name", "position", "department",
    "block", "periods", "block_start", "block_end", "check_in", "check_out", "permission_hours",
    "late_minutes", "early_minutes", "total_minutes", "cancelled_by_leave", "minutes_before_leave", "flag"];
  const esc = (v) => { const s = v == null ? "" : String(v); return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s; };
  const lines = rows.map((r) => [r.recordId, r.attendDate, r.day, r.employeeId, r.employeeName, r.position,
    r.department, r.blockKey, r.periods, r.blockStart, r.blockEnd, r.checkIn, r.checkOut, r.permissionHours,
    r.lateMinutes, r.earlyMinutes, r.totalMinutes, r.cancelledByLeave, r.minutesBeforeLeave, r.flag].map(esc).join(","));
  return "﻿" + [head.join(","), ...lines].join("\r\n");
}

/* ------------------------------------------------------------ late / early */

function LateEarlyScreen() {
  const [mode, setMode] = useStateR("calendar");       // calendar | payroll
  const [month, setMonth] = useStateR(() => {
    const n = new Date(); const d = new Date(n.getFullYear(), n.getMonth() - 1, 1);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}`;
  });
  const [gender, setGender] = useStateR("");
  const [empId, setEmpId] = useStateR("");
  const [includeCancelled, setIncludeCancelled] = useStateR(true);
  const [pageSize, setPageSize] = useStateR(25);
  const [data, setData] = useStateR(null);
  const [error, setError] = useStateR("");
  const [loading, setLoading] = useStateR(false);
  const [exporting, setExporting] = useStateR(false);

  const range = mode === "payroll" ? payMonthRange(month) : calendarMonthRange(month);

  function buildUrl(page, size) {
    const r = mode === "payroll" ? `payMonth=${encodeURIComponent(month)}` : `from=${range.from}&to=${range.to}`;
    return `/api/late-early?${r}`
      + (gender ? `&gender=${gender}` : "")
      + (empId.trim() ? `&employeeId=${encodeURIComponent(empId.trim())}` : "")
      + `&includeLeaveCancelled=${includeCancelled}&pageNumber=${page}&pageSize=${size}`;
  }

  async function load(page) {
    if (!range) { setError("اختر الشهر."); return; }
    setLoading(true); setError("");
    try { setData(await apiGet(buildUrl(page, pageSize))); }
    catch (e) { setError(e.message || "تعذّر جلب التأخر والخروج المبكر."); setData(null); }
    finally { setLoading(false); }
  }

  async function exportAll() {
    setExporting(true);
    try {
      const all = []; let p = 1, pages = 1;
      do { const d = await apiGet(buildUrl(p, 100)); all.push(...(d.items || [])); pages = d.totalPages || 1; p++; } while (p <= pages);
      const blob = new Blob([lateEarlyCsv(all)], { type: "text/csv;charset=utf-8" });
      const a = document.createElement("a");
      a.href = URL.createObjectURL(blob); a.download = `LateEarly_${month}${gender ? "_" + gender : ""}.csv`; a.click();
      URL.revokeObjectURL(a.href);
      toast(`تم تصدير ${all.length} صف`);
    } catch (e) { toast(`فشل التصدير: ${e.message}`); }
    finally { setExporting(false); }
  }

  useEffectR(() => { load(1); }, []);
  const items = (data && data.items) || [];

  return (
    <React.Fragment>
      <PageHead title="التأخر والخروج المبكر"
        desc="دقائق التأخر والخروج المبكر لكل موظف/يوم/مجموعة فترات بعد دقائق السماح، وفق قواعد الاحتساب السارية للشهر. المصدر: SmartHCM عبر HR API.">
        <button className="btn btn-soft" disabled={!data || exporting || !data.totalCount} onClick={exportAll}>
          <i className={"ti " + (exporting ? "ti-loader-2" : "ti-file-spreadsheet")} style={exporting ? { animation: "spin .7s linear infinite" } : null}></i>تصدير CSV
        </button>
      </PageHead>

      <div className="card">
        <div className="card-head">
          <h2>النطاق</h2>
          {range && <span className="sub">من <span className="lat">{range.from}</span> إلى <span className="lat">{range.to}</span></span>}
        </div>
        <div className="formrow">
          <div className="field">
            <label>نوع الفترة</label>
            <select value={mode} onChange={(e) => setMode(e.target.value)}>
              <option value="calendar">شهر ميلادي (١ – آخر الشهر)</option>
              <option value="payroll">فترة الرواتب (١٦ – ١٥)</option>
            </select>
          </div>
          <div className="field"><label>الشهر</label><input type="month" value={month} onChange={(e) => setMonth(e.target.value)} /></div>
          <div className="field">
            <label>الجنس</label>
            <select value={gender} onChange={(e) => setGender(e.target.value)}>
              <option value="">الكل</option><option value="M">رجال</option><option value="F">نساء</option>
            </select>
          </div>
          <div className="field"><label>الرقم الوظيفي (اختياري)</label><input value={empId} onChange={(e) => setEmpId(e.target.value)} className="lat" placeholder="كل الموظفين" /></div>
          <div className="field">
            <label>عدد الصفوف</label>
            <select value={pageSize} onChange={(e) => setPageSize(Number(e.target.value))}>{[25, 50, 100].map((n) => <option key={n} value={n}>{n}</option>)}</select>
          </div>
          <label style={{ display: "flex", gap: 7, alignItems: "center", alignSelf: "end", paddingBottom: 10, fontSize: 13 }}>
            <input type="checkbox" checked={includeCancelled} onChange={(e) => setIncludeCancelled(e.target.checked)} style={{ accentColor: "var(--color-primary)", width: 15, height: 15 }} />
            إظهار ما ألغاه الإذن الساعي
          </label>
          <button className="btn btn-primary" disabled={loading} onClick={() => load(1)}>
            <i className={"ti " + (loading ? "ti-loader-2" : "ti-report-search")} style={loading ? { animation: "spin .7s linear infinite" } : null}></i>جلب التقرير
          </button>
        </div>
        {error && <p className="error" style={{ color: "var(--color-danger-dark)", marginTop: 12 }}>{error}</p>}
      </div>

      {data && (
        <div className="stat-row" style={{ gridTemplateColumns: "repeat(4,1fr)", marginBottom: 18 }}>
          <div className="stat-tile"><div className="ic ic-danger"><i className="ti ti-clock-exclamation"></i></div><div className="meta"><div className="num">{MIN(data.totalMinutes)}</div><div className="lbl">إجمالي الدقائق (المبلغ)</div></div></div>
          <div className="stat-tile"><div className="ic ic-warning"><i className="ti ti-clock-pause"></i></div><div className="meta"><div className="num">{MIN(data.totalLateMinutes)}</div><div className="lbl">دقائق التأخر</div></div></div>
          <div className="stat-tile"><div className="ic ic-accent"><i className="ti ti-door-exit"></i></div><div className="meta"><div className="num">{MIN(data.totalEarlyMinutes)}</div><div className="lbl">دقائق الخروج المبكر</div></div></div>
          <div className="stat-tile"><div className="ic ic-info"><i className="ti ti-file-certificate"></i></div><div className="meta"><div className="num">{data.cancelledByLeaveCount}</div><div className="lbl">ألغاها الإذن الساعي</div></div></div>
        </div>
      )}

      {data && data.totalCount === 0 && <div className="card"><p className="muted" style={{ margin: 0 }}>لا يوجد تأخر أو خروج مبكر في النطاق المحدد. 🎉</p></div>}

      {data && data.totalCount > 0 && (
        <div className="card">
          <div className="card-head">
            <h2>المجموعات المتأخرة</h2>
            <span className="sub"><span className="lat">{items.length}</span> من <span className="lat">{data.totalCount}</span> صف</span>
          </div>
          <div className="table-wrap">
            <table className="data">
              <thead>
                <tr>
                  <th>التاريخ</th><th>الرقم الوظيفي</th><th>الاسم</th><th>المجموعة</th><th>الفترات</th>
                  <th>بداية – نهاية</th><th>الحضور</th><th>الانصراف</th><th>الإذن الساعي</th>
                  <th>دقائق التأخر</th><th>الخروج المبكر</th><th>المبلغ (دقيقة)</th><th>ملاحظة</th>
                </tr>
              </thead>
              <tbody>
                {items.map((r) => (
                  <tr key={r.recordId} className={r.cancelledByLeave ? "row-success" : r.flag ? "row-warning" : ""}>
                    <td className="lat">{r.attendDate}<div className="muted" style={{ fontSize: 11 }}>{dayNameAr(r.attendDate)}</div></td>
                    <td><span className="id-badge">{r.employeeId}</span></td>
                    <td><div className="emp-cell"><div className="av" style={{ background: avColor(r.employeeId) }}>{firstLetter(r.employeeName)}</div><span dir="auto">{r.employeeName || "—"}</span></div></td>
                    <td><span className={r.blockKey === "GRP_MORNING" ? "chip chip-primary" : r.blockKey === "GRP_EVENING" ? "chip chip-warn" : "chip"}>{d33GroupLabel(r.blockKey)}</span></td>
                    <td dir="auto">{r.periods || "—"}</td>
                    <td className="lat">{r.blockStart || "—"} – {r.blockEnd || "—"}</td>
                    <td className="lat">{r.checkIn || <span className="muted">—</span>}</td>
                    <td className="lat">{r.checkOut || <span className="muted">—</span>}</td>
                    <td className="lat">{r.permissionHours ? r.permissionHours : "—"}</td>
                    <td className="lat">{MIN(r.lateMinutes)}</td>
                    <td className="lat">{MIN(r.earlyMinutes)}</td>
                    <td className="lat" style={{ fontWeight: 700 }}>{MIN(r.totalMinutes)}</td>
                    <td>
                      {r.cancelledByLeave
                        ? <span className="chip chip-success" title={`كانت ${MIN(r.minutesBeforeLeave)} دقيقة قبل الإذن`}><i className="ti ti-file-certificate"></i> ألغاها الإذن</span>
                        : r.flag === "NO_BREAKUP_ROW"
                          ? <span className="chip chip-warn"><i className="ti ti-clock-question"></i> لا يوجد توزيع فترات</span>
                          : <span className="muted">—</span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="pager">
            <button className="btn btn-ghost btn-sm" disabled={loading || !data.hasPrevious} onClick={() => load(data.pageNumber - 1)}><i className="ti ti-chevron-right"></i>السابق</button>
            <span className="lbl">صفحة <span className="lat">{data.pageNumber}</span> من <span className="lat">{data.totalPages}</span></span>
            <button className="btn btn-ghost btn-sm" disabled={loading || !data.hasNext} onClick={() => load(data.pageNumber + 1)}>التالي<i className="ti ti-chevron-left"></i></button>
          </div>
        </div>
      )}
    </React.Fragment>
  );
}

/* ------------------------------------------------------------ calculation rules */

function ruleDisplay(rule, value) {
  if (rule.type === "bool") return value === "true" ? "مفعّل" : "متوقف";
  if (rule.type === "int_list") return value.split(",").join("، ");
  return value;
}

function RuleInput({ rule, value, onChange }) {
  if (rule.type === "bool") {
    return (
      <label style={{ display: "flex", gap: 7, alignItems: "center", fontSize: 13 }}>
        <input type="checkbox" checked={value === "true"} onChange={(e) => onChange(e.target.checked ? "true" : "false")}
          style={{ accentColor: "var(--color-primary)", width: 17, height: 17 }} />
        {value === "true" ? "مفعّل" : "متوقف"}
      </label>
    );
  }
  if (rule.type === "time") return <input type="time" className="lat" value={value} onChange={(e) => onChange(e.target.value)} style={{ width: 130 }} />;
  if (rule.type === "int") return <input type="number" className="lat" value={value} min={rule.min ?? undefined} max={rule.max ?? undefined} onChange={(e) => onChange(e.target.value)} style={{ width: 110 }} />;
  return <input className="lat" value={value} onChange={(e) => onChange(e.target.value)} placeholder="1,2,3" style={{ width: 180 }} />;
}

function RuleHistoryPanel({ rule, onClose }) {
  const [rows, setRows] = useStateR(null);
  const [error, setError] = useStateR("");
  useEffectR(() => {
    apiGet(`/api/attendance-rules/history?key=${encodeURIComponent(rule.key)}`)
      .then((d) => setRows(d || [])).catch((e) => setError(e.message || "تعذّر جلب السجل."));
  }, [rule.key]);
  return (
    <div className="card fade-in">
      <div className="card-head">
        <h2>سجل التعديلات — {rule.labelAr}</h2>
        <button className="btn btn-ghost btn-sm" onClick={onClose}><i className="ti ti-x"></i>إغلاق</button>
      </div>
      {error && <p className="error" style={{ color: "var(--color-danger-dark)", margin: 0 }}>{error}</p>}
      {!rows && !error && <p className="muted">جارٍ التحميل...</p>}
      {rows && rows.length === 0 && <p className="muted" style={{ margin: 0 }}>لم تُعدّل هذه القاعدة بعد — تُستخدم القيمة الافتراضية ({ruleDisplay(rule, rule.defaultValue)}).</p>}
      {rows && rows.length > 0 && (
        <div className="table-wrap">
          <table className="data">
            <thead><tr><th>يسري من</th><th>القيمة</th><th>بواسطة</th><th>التاريخ</th><th>ملاحظة</th></tr></thead>
            <tbody>
              {rows.map((r, i) => (
                <tr key={i}>
                  <td className="lat">{r.effectiveFrom}</td>
                  <td>{r.value == null ? <span className="chip chip-muted">الافتراضي</span> : <b className="lat">{ruleDisplay(rule, r.value)}</b>}</td>
                  <td className="lat">{r.updatedBy || "—"}</td>
                  <td className="lat">{r.updatedAt ? String(r.updatedAt).replace("T", " ").slice(0, 16) : "—"}</td>
                  <td dir="auto">{r.remarks || <span className="muted">—</span>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function AttendanceRulesScreen() {
  const [viewMonth, setViewMonth] = useStateR(currentPayMonth());
  const [data, setData] = useStateR(null);
  const [canEdit, setCanEdit] = useStateR(false);
  const [editing, setEditing] = useStateR(false);
  const [draft, setDraft] = useStateR({});      // key → value text, or null = back to default
  const [effectiveFrom, setEffectiveFrom] = useStateR("");
  const [remarks, setRemarks] = useStateR("");
  const [saving, setSaving] = useStateR(false);
  const [error, setError] = useStateR("");
  const [errors, setErrors] = useStateR([]);
  const [history, setHistory] = useStateR(null);

  async function load(month) {
    setError(""); setData(null);
    try { setData(await apiGet(`/api/attendance-rules?payMonth=${encodeURIComponent(month)}`)); }
    catch (e) { setError(e.message || "تعذّر جلب القواعد."); }
  }
  useEffectR(() => { load(viewMonth); }, [viewMonth]);
  useEffectR(() => {
    apiGet("/api/attendance-rules/rights").then((r) => setCanEdit(!!(r && r.canEdit))).catch(() => setCanEdit(false));
  }, []);

  function startEdit() {
    setDraft({});
    setEffectiveFrom(data.earliestEditableMonth > viewMonth ? data.earliestEditableMonth : viewMonth);
    setRemarks(""); setErrors([]); setEditing(true);
  }

  /* Rules whose edited value differs from what is in force (reset = null). */
  function changes() {
    return data.rules.flatMap((r) => {
      if (!(r.key in draft)) return [];
      const v = draft[r.key];
      if (v === null) return r.isDefault ? [] : [{ rule: r, value: null }];
      return String(v).trim() === r.value ? [] : [{ rule: r, value: String(v).trim() }];
    });
  }

  async function save() {
    const list = changes();
    if (!list.length) { toast("لا توجد تغييرات للحفظ."); return; }
    const summary = list.map(({ rule, value }) =>
      `• ${rule.labelAr}: ${ruleDisplay(rule, rule.value)} ← ${value === null ? `الافتراضي (${ruleDisplay(rule, rule.defaultValue)})` : ruleDisplay(rule, value)}`).join("\n");
    if (!window.confirm(`حفظ ${list.length} تغيير، يسري من ${effectiveFrom}:\n\n${summary}\n\nيُطبّق على حساب الغياب والتأخر من هذا الشهر فما بعده.`)) return;
    setSaving(true); setErrors([]);
    try {
      await apiSend("/api/attendance-rules", "PUT", {
        effectiveFrom, remarks: remarks.trim() || null,
        rules: list.map(({ rule, value }) => ({ key: rule.key, value })),
      });
      toast(`تم حفظ ${list.length} تغيير`);
      setEditing(false);
      if (viewMonth === effectiveFrom) load(viewMonth); else setViewMonth(effectiveFrom);
    } catch (e) {
      setErrors((e.data && Array.isArray(e.data.errors) && e.data.errors.length) ? e.data.errors : [e.message || "تعذّر الحفظ."]);
    } finally { setSaving(false); }
  }

  const pending = editing && data ? changes().length : 0;
  const groups = data ? Array.from(new Set(data.rules.map((r) => r.group))) : [];

  return (
    <React.Fragment>
      <PageHead title="قواعد الاحتساب"
        desc="القواعد التي يُحسب بها الغياب (D33) والتأخر والخروج المبكر. يعدّلها مسؤولو الموارد البشرية فقط، وتسري من شهر محدد دون تغيير حساب الأشهر السابقة." />

      <div className="card">
        <div className="formrow">
          <div className="field"><label>شهر الرواتب</label>
            <input type="month" value={viewMonth} disabled={editing} onChange={(e) => setViewMonth(e.target.value)} />
          </div>
          {data && <span className="sub" style={{ alignSelf: "end", paddingBottom: 10 }}>
            القواعد السارية لفترة <span className="lat">{payMonthRange(viewMonth) ? `${payMonthRange(viewMonth).from} → ${payMonthRange(viewMonth).to}` : viewMonth}</span>
            {" · "}<span className="lat">{data.rules.filter((r) => !r.isDefault).length}</span> معدّلة
          </span>}
          <div className="spacer" style={{ flex: 1 }}></div>
          {canEdit && !editing && data && <button className="btn btn-primary" onClick={startEdit}><i className="ti ti-edit"></i>تعديل القواعد</button>}
          {!canEdit && <span className="chip chip-muted" style={{ alignSelf: "end", marginBottom: 8 }}><i className="ti ti-lock"></i> للعرض فقط — التعديل لمسؤولي الموارد البشرية</span>}
        </div>
        {error && <p className="error" style={{ color: "var(--color-danger-dark)", marginTop: 12 }}>{error}</p>}
        {!data && !error && <p className="muted">جارٍ التحميل...</p>}

        {editing && (
          <div style={{ background: "var(--color-bg)", borderRadius: 8, padding: 14, marginTop: 14 }}>
            <div className="formrow">
              <div className="field"><label>يسري من شهر</label>
                <input type="month" value={effectiveFrom} min={data.earliestEditableMonth} onChange={(e) => setEffectiveFrom(e.target.value)} />
              </div>
              <div className="field" style={{ flex: 2 }}><label>ملاحظة (اختياري)</label>
                <input value={remarks} maxLength={500} onChange={(e) => setRemarks(e.target.value)} placeholder="سبب التعديل" />
              </div>
            </div>
            <p className="muted" style={{ fontSize: 12.5, margin: "10px 0 0" }}>
              <i className="ti ti-info-circle"></i> القواعد الجديدة تسري من الشهر المختار فما بعده، والأشهر السابقة تُحسب بقواعدها.
              لا يمكن اختيار شهر قبل <span className="lat">{data.earliestEditableMonth}</span>.
            </p>
            {errors.length > 0 && (
              <ul className="error" style={{ color: "var(--color-danger-dark)", margin: "10px 0 0", paddingInlineStart: 18 }}>
                {errors.map((e, i) => <li key={i}>{e}</li>)}
              </ul>
            )}
          </div>
        )}
      </div>

      {data && groups.map((g) => (
        <div className="card" key={g}>
          <div className="card-head"><h2>{ruleGroupLabel(g)}</h2></div>
          <div className="table-wrap">
            <table className="data">
              <thead><tr><th style={{ width: "30%" }}>القاعدة</th><th>القيمة</th><th>الافتراضي</th><th>الحالة</th><th></th></tr></thead>
              <tbody>
                {data.rules.filter((r) => r.group === g).map((r) => {
                  const edited = r.key in draft;
                  const value = edited ? (draft[r.key] === null ? r.defaultValue : draft[r.key]) : r.value;
                  const changed = editing && changes().some((c) => c.rule.key === r.key);
                  return (
                    <tr key={r.key} className={changed ? "row-warning" : ""}>
                      <td><b>{r.labelAr}</b><div className="muted" style={{ fontSize: 12, marginTop: 3 }}>{r.descriptionAr}</div></td>
                      <td>
                        {editing
                          ? <RuleInput rule={r} value={value} onChange={(v) => setDraft({ ...draft, [r.key]: v })} />
                          : <b className="lat">{ruleDisplay(r, r.value)}</b>}
                      </td>
                      <td className="lat muted">{ruleDisplay(r, r.defaultValue)}</td>
                      <td>
                        {editing && draft[r.key] === null
                          ? <span className="chip chip-info">سيُعاد للافتراضي</span>
                          : r.isDefault
                            ? <span className="chip chip-muted">افتراضي</span>
                            : <span className="chip chip-warn" title={`بواسطة ${r.updatedBy || "—"}`}>معدّلة منذ <span className="lat">{r.effectiveFrom}</span></span>}
                      </td>
                      <td style={{ whiteSpace: "nowrap" }}>
                        {editing && !r.isDefault && draft[r.key] !== null && (
                          <button className="btn btn-ghost btn-sm" title="إرجاع للقيمة الافتراضية" onClick={() => setDraft({ ...draft, [r.key]: null })}><i className="ti ti-arrow-back-up"></i>الافتراضي</button>
                        )}
                        {!editing && <button className="btn btn-ghost btn-sm" title="سجل التعديلات" onClick={() => setHistory(r)}><i className="ti ti-history"></i></button>}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      ))}

      {editing && (
        <div className="card">
          <div className="pager" style={{ marginTop: 0 }}>
            <button className="btn btn-primary" disabled={saving || pending === 0} onClick={save}>
              <i className={"ti " + (saving ? "ti-loader-2" : "ti-device-floppy")} style={saving ? { animation: "spin .7s linear infinite" } : null}></i>
              حفظ (<span className="lat">{pending}</span>)
            </button>
            <button className="btn btn-ghost" disabled={saving} onClick={() => { setEditing(false); setDraft({}); setErrors([]); }}><i className="ti ti-x"></i>إلغاء</button>
          </div>
        </div>
      )}

      {history && !editing && <RuleHistoryPanel rule={history} onClose={() => setHistory(null)} />}
    </React.Fragment>
  );
}

Object.assign(window, { LateEarlyScreen, AttendanceRulesScreen });
