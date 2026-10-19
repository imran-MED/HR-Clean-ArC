/* ============================================================ DAWAM — D33 deductions + absence rates
   Two screens over the HR API's teaching-staff absence endpoints (proxied by
   HrAuth, bearer token forwarded):
     • D33DeductionsScreen — /api/absences: one row per employee / day / period
       block with the deduction amount, paged server-side, with the payroll
       period's grand total. Approval and posting to payroll come later.
     • AbsenceRatesScreen  — /api/absence-rates: the position × period monthly
       amounts in force for a pay month, editable by HR admins (SmartHCM Edit
       right on PAY/S017, checked by the HR API) as a new version from a chosen
       month; plus the SmartHCM reference data (periods, grades, positions). */
const { useState: useStateD, useEffect: useEffectD } = React;

const SAR = (n) =>
  Number(n || 0).toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/* The payroll period closes on the 15th: on the 16th onward we are already in
   next month's period. Returns "yyyy-MM". */
function currentPayMonth() {
  const n = new Date();
  const d = new Date(n.getFullYear(), n.getMonth() + (n.getDate() > 15 ? 1 : 0), 1);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}`;
}

/* "2026-09" → { from: "2026-08-16", to: "2026-09-15" } */
function payMonthRange(pm) {
  if (!/^\d{4}-\d{2}$/.test(pm || "")) return null;
  const [y, m] = pm.split("-").map(Number);
  const prev = new Date(y, m - 2, 16);
  const iso = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
  return { from: iso(prev), to: iso(new Date(y, m - 1, 15)) };
}

function d33GroupLabel(key) {
  if (key === "GRP_MORNING") return "صباحية";
  if (key === "GRP_EVENING") return "مسائية";
  return key;
}

function D33Flag({ r }) {
  if (r.flag === "NO_RATE") {
    return (
      <span className="chip chip-danger" title="لا يوجد مبلغ لهذه الفترة لهذا المسمى الوظيفي — أضِفه في شاشة مبالغ فترات الغياب. المبلغ المعروض لا يشمل هذه الفترة.">
        <i className="ti ti-coin-off"></i> لا يوجد مبلغ
      </span>
    );
  }
  if (r.flag === "SYNTHESIZED_RECALCULATE") {
    return (
      <span className="chip chip-danger" title={`لا توجد بيانات حضور لهذا اليوم — نُسخ النمط من ${r.patternFrom || "—"}. أعد احتساب اليوم في SmartHCM.`}>
        <i className="ti ti-alert-triangle"></i> مُستنتَج — أعد الاحتساب
      </span>
    );
  }
  if (r.flag === "NO_BREAKUP_ROW") {
    return (
      <span className="chip chip-warn" title="لا يوجد توزيع أوقات لهذه الفترة في هذا الشهر.">
        <i className="ti ti-clock-question"></i> لا يوجد توزيع فترات
      </span>
    );
  }
  return <span className="muted">—</span>;
}

function toCsv(rows) {
  const head = ["absence_id", "attend_date", "day", "employee_id", "employee_name", "position",
    "department", "group", "periods", "period_count", "required_hours", "leave_hours",
    "absent_hours", "deduction_amount", "flag", "pattern_from"];
  const esc = (v) => {
    const s = v == null ? "" : String(v);
    return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  const lines = rows.map((r) => [r.absenceId, r.attendDate, r.day, r.employeeId, r.employeeName,
    r.position, r.department, r.groupKey, r.periods, r.periodCount, r.requiredHours,
    r.leaveHours, r.absentHours, r.deductionAmount, r.flag, r.patternFrom].map(esc).join(","));
  return "﻿" + [head.join(","), ...lines].join("\r\n");
}

function D33DeductionsScreen() {
  const [payMonth, setPayMonth] = useStateD(currentPayMonth());
  const [gender, setGender] = useStateD("");
  const [empId, setEmpId] = useStateD("");
  const [includeSynth, setIncludeSynth] = useStateD(true);
  const [pageSize, setPageSize] = useStateD(25);
  const [page, setPage] = useStateD(1);
  const [data, setData] = useStateD(null);
  const [error, setError] = useStateD("");
  const [loading, setLoading] = useStateD(false);
  const [exporting, setExporting] = useStateD(false);

  function buildUrl(pageNumber, size) {
    return `/api/absences?payMonth=${encodeURIComponent(payMonth)}`
      + (gender ? `&gender=${gender}` : "")
      + (empId.trim() ? `&employeeId=${encodeURIComponent(empId.trim())}` : "")
      + `&includeSynthesized=${includeSynth}`
      + `&pageNumber=${pageNumber}&pageSize=${size}`;
  }

  async function load(pageNumber) {
    if (!payMonthRange(payMonth)) { setError("اختر شهر الرواتب."); return; }
    setLoading(true); setError("");
    try {
      const d = await apiGet(buildUrl(pageNumber, pageSize));
      setData(d); setPage(d.pageNumber || pageNumber);
    } catch (e) {
      setError(e.message || "تعذّر جلب الخصومات.");
      setData(null);
    } finally {
      setLoading(false);
    }
  }

  /* Pull every page (100 at a time) and download one CSV — for checking the
     month against the old Excel sheet. */
  async function exportAll() {
    setExporting(true);
    try {
      const all = [];
      let p = 1, pages = 1;
      do {
        const d = await apiGet(buildUrl(p, 100));
        all.push(...(d.items || []));
        pages = d.totalPages || 1;
        p++;
      } while (p <= pages);
      const blob = new Blob([toCsv(all)], { type: "text/csv;charset=utf-8" });
      const a = document.createElement("a");
      a.href = URL.createObjectURL(blob);
      a.download = `D33_${payMonth}${gender ? "_" + gender : ""}.csv`;
      a.click();
      URL.revokeObjectURL(a.href);
      toast(`تم تصدير ${all.length} صف`);
    } catch (e) {
      toast(`فشل التصدير: ${e.message}`);
    } finally {
      setExporting(false);
    }
  }

  useEffectD(() => { load(1); }, []);

  const range = payMonthRange(payMonth);
  const items = (data && data.items) || [];
  const pageTotal = items.reduce((s, r) => s + Number(r.deductionAmount || 0), 0);
  const flagged = items.filter((r) => r.flag).length;

  return (
    <React.Fragment>
      <PageHead title="خصومات الغياب (D33)"
        desc="غياب الهيئة التعليمية والإدارية لفترة الرواتب (١٦ من الشهر السابق حتى ١٥) مع مبلغ الخصم لكل فترة غياب. المصدر: SmartHCM عبر HR API.">
        <button className="btn btn-soft" disabled={!data || exporting || !data.totalCount} onClick={exportAll}>
          <i className={"ti " + (exporting ? "ti-loader-2" : "ti-file-spreadsheet")}
             style={exporting ? { animation: "spin .7s linear infinite" } : null}></i>تصدير CSV
        </button>
      </PageHead>

      <div className="card">
        <div className="card-head">
          <h2>فترة الرواتب</h2>
          {range && <span className="sub">من <span className="lat">{range.from}</span> إلى <span className="lat">{range.to}</span></span>}
        </div>
        <div className="formrow">
          <div className="field"><label>شهر الرواتب</label><input type="month" value={payMonth} onChange={(e) => setPayMonth(e.target.value)} /></div>
          <div className="field">
            <label>الجنس</label>
            <select value={gender} onChange={(e) => setGender(e.target.value)}>
              <option value="">الكل</option>
              <option value="M">رجال</option>
              <option value="F">نساء</option>
            </select>
          </div>
          <div className="field"><label>الرقم الوظيفي (اختياري)</label><input value={empId} onChange={(e) => setEmpId(e.target.value)} className="lat" placeholder="كل الموظفين" /></div>
          <div className="field">
            <label>عدد الصفوف</label>
            <select value={pageSize} onChange={(e) => setPageSize(Number(e.target.value))}>
              {[25, 50, 100].map((n) => <option key={n} value={n}>{n}</option>)}
            </select>
          </div>
          <label style={{ display: "flex", gap: 7, alignItems: "center", alignSelf: "end", paddingBottom: 10, fontSize: 13 }}>
            <input type="checkbox" checked={includeSynth} onChange={(e) => setIncludeSynth(e.target.checked)}
              style={{ accentColor: "var(--color-primary)", width: 15, height: 15 }} />
            تضمين الأيام المُستنتَجة
          </label>
          <button className="btn btn-primary" disabled={loading} onClick={() => load(1)}>
            <i className={"ti " + (loading ? "ti-loader-2" : "ti-report-search")}
               style={loading ? { animation: "spin .7s linear infinite" } : null}></i>جلب الخصومات
          </button>
        </div>
        {error && <p className="error" style={{ color: "var(--color-danger-dark)", marginTop: 12 }}>{error}</p>}
      </div>

      {data && (
        <div className="stat-row" style={{ gridTemplateColumns: "repeat(4,1fr)", marginBottom: 18 }}>
          <div className="stat-tile"><div className="ic ic-danger"><i className="ti ti-receipt-2"></i></div><div className="meta"><div className="num">{SAR(data.grandTotalAmount)}</div><div className="lbl">إجمالي خصم الشهر (ر.س)</div></div></div>
          <div className="stat-tile"><div className="ic ic-primary"><i className="ti ti-list-numbers"></i></div><div className="meta"><div className="num">{data.totalCount}</div><div className="lbl">فترة غياب</div></div></div>
          <div className="stat-tile"><div className="ic ic-accent"><i className="ti ti-sum"></i></div><div className="meta"><div className="num">{SAR(pageTotal)}</div><div className="lbl">مجموع هذه الصفحة (ر.س)</div></div></div>
          <div className="stat-tile"><div className="ic ic-warning"><i className="ti ti-alert-triangle"></i></div><div className="meta"><div className="num">{flagged}</div><div className="lbl">صفوف تحتاج مراجعة (هذه الصفحة)</div></div></div>
        </div>
      )}

      {data && data.totalCount === 0 && (
        <div className="card"><p className="muted" style={{ margin: 0 }}>لا يوجد غياب في فترة الرواتب المحددة. 🎉</p></div>
      )}

      {data && data.totalCount > 0 && (
        <div className="card">
          <div className="card-head">
            <h2>فترات الغياب</h2>
            <span className="sub">
              <span className="lat">{items.length}</span> من <span className="lat">{data.totalCount}</span> صف
            </span>
          </div>
          <div className="table-wrap">
            <table className="data">
              <thead>
                <tr>
                  <th>التاريخ</th><th>الرقم الوظيفي</th><th>الاسم</th><th>المنصب</th>
                  <th>القسم</th><th>المجموعة</th><th>الفترات</th>
                  <th>المطلوبة (س)</th><th>الإجازة (س)</th><th>الغياب (س)</th>
                  <th>مبلغ الخصم (ر.س)</th><th>ملاحظة</th>
                </tr>
              </thead>
              <tbody>
                {items.map((r) => (
                  <tr key={r.absenceId} className={r.flag === "SYNTHESIZED_RECALCULATE" || r.flag === "NO_RATE" ? "row-danger" : r.flag ? "row-warning" : ""}>
                    <td className="lat">{r.attendDate}<div className="muted" style={{ fontSize: 11 }}>{dayNameAr(r.attendDate)}</div></td>
                    <td><span className="id-badge">{r.employeeId}</span></td>
                    <td><div className="emp-cell"><div className="av" style={{ background: avColor(r.employeeId) }}>{firstLetter(r.employeeName)}</div><span dir="auto">{r.employeeName || "—"}</span></div></td>
                    <td dir="auto">{r.position || <span className="muted">—</span>}</td>
                    <td dir="auto">{r.department || <span className="muted">—</span>}</td>
                    <td><span className={r.groupKey === "GRP_MORNING" ? "chip chip-primary" : r.groupKey === "GRP_EVENING" ? "chip chip-warn" : "chip"}>{d33GroupLabel(r.groupKey)}</span></td>
                    <td className="lat">{r.periods || "—"} <span className="muted">({r.periodCount})</span></td>
                    <td className="lat">{r.requiredHours}</td>
                    <td className="lat">{r.leaveHours}</td>
                    <td className="lat">{r.absentHours}</td>
                    <td className="lat" style={{ fontWeight: 700 }}>{SAR(r.deductionAmount)}</td>
                    <td><D33Flag r={r} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="pager">
            <button className="btn btn-ghost btn-sm" disabled={loading || !data.hasPrevious} onClick={() => load(page - 1)}><i className="ti ti-chevron-right"></i>السابق</button>
            <span className="lbl">صفحة <span className="lat">{data.pageNumber}</span> من <span className="lat">{data.totalPages}</span></span>
            <button className="btn btn-ghost btn-sm" disabled={loading || !data.hasNext} onClick={() => load(page + 1)}>التالي<i className="ti ti-chevron-left"></i></button>
          </div>
        </div>
      )}

      <div className="card">
        <p className="muted" style={{ margin: 0, fontSize: 13 }}>
          <i className="ti ti-info-circle"></i> اعتماد الغياب وترحيل إجمالي كل موظف إلى الرواتب (D33) سيُضاف لاحقاً — هذه الشاشة للعرض والمراجعة حالياً.
        </p>
      </div>
    </React.Fragment>
  );
}

/* ------------------------------------------------------------ absence rates */

const cellKey = (positionId, periodId) => `${positionId}:${periodId}`;

/* Version history of one position / period, newest first. */
function RateHistory({ positionId, periodId, title, onClose }) {
  const [rows, setRows] = useStateD(null);
  const [error, setError] = useStateD("");

  useEffectD(() => {
    setRows(null); setError("");
    apiGet(`/api/absence-rates/history?positionId=${positionId}&periodId=${periodId}`)
      .then((d) => setRows(d || []))
      .catch((e) => setError(e.message || "تعذّر جلب السجل."));
  }, [positionId, periodId]);

  return (
    <div className="card fade-in">
      <div className="card-head">
        <h2>سجل التعديلات — <span dir="auto">{title}</span></h2>
        <button className="btn btn-ghost btn-sm" onClick={onClose}><i className="ti ti-x"></i>إغلاق</button>
      </div>
      {error && <p className="error" style={{ color: "var(--color-danger-dark)", margin: 0 }}>{error}</p>}
      {!rows && !error && <p className="muted">جارٍ التحميل...</p>}
      {rows && rows.length === 0 && <p className="muted" style={{ margin: 0 }}>لا يوجد سجل لهذه الخانة.</p>}
      {rows && rows.length > 0 && (
        <div className="table-wrap">
          <table className="data">
            <thead><tr><th>يسري من</th><th>المبلغ الشهري (ر.س)</th><th>خصم اليوم (ر.س)</th><th>بواسطة</th><th>التاريخ</th><th>ملاحظة</th></tr></thead>
            <tbody>
              {rows.map((r, i) => (
                <tr key={i}>
                  <td className="lat">{r.effectiveFrom}</td>
                  <td className="lat">{r.monthlyAmount == null ? <span className="chip chip-muted">أُلغي</span> : <b>{SAR(r.monthlyAmount)}</b>}</td>
                  <td className="lat">{r.dailyRate == null ? "—" : SAR(r.dailyRate)}</td>
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

/* Position × period matrix of monthly amounts, editable by HR admins. */
function RateMatrix({ positions, periods }) {
  const [viewMonth, setViewMonth] = useStateD(currentPayMonth());
  const [matrix, setMatrix] = useStateD(null);
  const [canEdit, setCanEdit] = useStateD(false);
  const [editing, setEditing] = useStateD(false);
  const [draft, setDraft] = useStateD({});          // cellKey → string typed in the input
  const [extraRows, setExtraRows] = useStateD([]);  // positions added while editing
  const [addPos, setAddPos] = useStateD("");
  const [effectiveFrom, setEffectiveFrom] = useStateD("");
  const [remarks, setRemarks] = useStateD("");
  const [saving, setSaving] = useStateD(false);
  const [error, setError] = useStateD("");
  const [errors, setErrors] = useStateD([]);
  const [history, setHistory] = useStateD(null);    // { positionId, periodId, title }

  async function load(month) {
    setError(""); setMatrix(null);
    try {
      setMatrix(await apiGet(`/api/absence-rates?payMonth=${encodeURIComponent(month)}`));
    } catch (e) {
      setError(e.message || "تعذّر جلب المبالغ.");
    }
  }

  useEffectD(() => { load(viewMonth); }, [viewMonth]);
  useEffectD(() => {
    apiGet("/api/absence-rates/rights").then((r) => setCanEdit(!!(r && r.canEdit))).catch(() => setCanEdit(false));
  }, []);

  const posTitle = (id) => ((positions || []).find((p) => Number(p.positionId) === id) || {}).title || `#${id}`;
  const per = (id) => (periods || []).find((p) => p.periodId === id) || {};
  const cells = {};
  (matrix ? matrix.cells : []).forEach((c) => { cells[cellKey(c.positionId, c.periodId)] = c; });
  const rows = matrix ? [...matrix.positionIds, ...extraRows.filter((id) => !matrix.positionIds.includes(id))] : [];
  const cols = matrix ? matrix.periodIds : [];

  function startEdit() {
    const d = {};
    rows.forEach((pos) => cols.forEach((pid) => {
      const c = cells[cellKey(pos, pid)];
      d[cellKey(pos, pid)] = c && c.monthlyAmount != null ? String(c.monthlyAmount) : "";
    }));
    setDraft(d);
    setEffectiveFrom(matrix.earliestEditableMonth > viewMonth ? matrix.earliestEditableMonth : viewMonth);
    setRemarks(""); setErrors([]); setEditing(true);
  }

  function cancelEdit() { setEditing(false); setExtraRows([]); setErrors([]); }

  /* Cells whose typed value differs from what is in force: empty = withdraw. */
  function changes() {
    const out = [];
    Object.entries(draft).forEach(([key, text]) => {
      const [pos, pid] = key.split(":").map(Number);
      const before = cells[key] && cells[key].monthlyAmount != null ? Number(cells[key].monthlyAmount) : null;
      const after = String(text).trim() === "" ? null : Number(text);
      if (after !== before && !(after === null && before === null)) {
        out.push({ positionId: pos, periodId: pid, monthlyAmount: after, before });
      }
    });
    return out;
  }

  async function save() {
    const list = changes();
    if (!list.length) { toast("لا توجد تغييرات للحفظ."); return; }
    const bad = list.find((c) => c.monthlyAmount !== null && !(c.monthlyAmount > 0));
    if (bad) { setErrors([`مبلغ غير صالح: ${posTitle(bad.positionId)} / ${per(bad.periodId).shortName || bad.periodId}`]); return; }

    const summary = list.map((c) =>
      `• ${posTitle(c.positionId)} — ${per(c.periodId).shortName || "P" + c.periodId}: `
      + `${c.before == null ? "لا يوجد" : SAR(c.before)} ← ${c.monthlyAmount == null ? "إلغاء" : SAR(c.monthlyAmount)}`).join("\n");
    if (!window.confirm(`حفظ ${list.length} تغيير، يسري من ${effectiveFrom}:\n\n${summary}`)) return;

    setSaving(true); setErrors([]);
    try {
      await apiSend("/api/absence-rates", "PUT", {
        effectiveFrom,
        remarks: remarks.trim() || null,
        cells: list.map(({ positionId, periodId, monthlyAmount }) => ({ positionId, periodId, monthlyAmount })),
      });
      toast(`تم حفظ ${list.length} تغيير`);
      setEditing(false); setExtraRows([]);
      if (viewMonth === effectiveFrom) load(viewMonth); else setViewMonth(effectiveFrom);
    } catch (e) {
      const list2 = (e.data && Array.isArray(e.data.errors) && e.data.errors.length) ? e.data.errors : [e.message || "تعذّر الحفظ."];
      setErrors(list2);
    } finally {
      setSaving(false);
    }
  }

  function addRow() {
    const id = Number(addPos);
    if (!id || rows.includes(id)) return;
    setExtraRows([...extraRows, id]);
    const d = { ...draft };
    cols.forEach((pid) => { d[cellKey(id, pid)] = ""; });
    setDraft(d); setAddPos("");
  }

  const pending = editing ? changes().length : 0;

  return (
    <div className="fade-in">
      <div className="formrow" style={{ marginBottom: 14 }}>
        <div className="field"><label>شهر الرواتب</label>
          <input type="month" value={viewMonth} disabled={editing} onChange={(e) => setViewMonth(e.target.value)} />
        </div>
        {matrix && <span className="sub" style={{ alignSelf: "end", paddingBottom: 10 }}>
          المبالغ السارية لفترة <span className="lat">{payMonthRange(viewMonth) ? `${payMonthRange(viewMonth).from} → ${payMonthRange(viewMonth).to}` : viewMonth}</span>
        </span>}
        <div className="spacer" style={{ flex: 1 }}></div>
        {canEdit && !editing && matrix && (
          <button className="btn btn-primary" onClick={startEdit}><i className="ti ti-edit"></i>تعديل المبالغ</button>
        )}
        {!canEdit && (
          <span className="chip chip-muted" style={{ alignSelf: "end", marginBottom: 8 }}><i className="ti ti-lock"></i> للعرض فقط — التعديل لمسؤولي الموارد البشرية</span>
        )}
      </div>

      {error && <p className="error" style={{ color: "var(--color-danger-dark)" }}>{error}</p>}
      {!matrix && !error && <p className="muted">جارٍ التحميل...</p>}

      {editing && (
        <div className="card" style={{ background: "var(--color-bg)", marginBottom: 14 }}>
          <div className="formrow">
            <div className="field"><label>يسري من شهر</label>
              <input type="month" value={effectiveFrom} min={matrix.earliestEditableMonth} onChange={(e) => setEffectiveFrom(e.target.value)} />
            </div>
            <div className="field" style={{ flex: 2 }}><label>ملاحظة (اختياري)</label>
              <input value={remarks} maxLength={500} onChange={(e) => setRemarks(e.target.value)} placeholder="سبب التعديل" />
            </div>
            <div className="field"><label>إضافة مسمى وظيفي</label>
              <select value={addPos} onChange={(e) => setAddPos(e.target.value)}>
                <option value="">— اختر —</option>
                {(positions || []).filter((p) => !rows.includes(Number(p.positionId)))
                  .map((p) => <option key={p.positionId} value={p.positionId}>{p.title || p.positionId} ({p.positionId})</option>)}
              </select>
            </div>
            <button className="btn btn-soft btn-sm" style={{ alignSelf: "end" }} disabled={!addPos} onClick={addRow}><i className="ti ti-plus"></i>إضافة</button>
          </div>
          <p className="muted" style={{ fontSize: 12.5, margin: "10px 0 0" }}>
            <i className="ti ti-info-circle"></i> المبالغ الجديدة تسري من الشهر المختار فما بعده، والأشهر السابقة تبقى بمبالغها. اترك الخانة فارغة لإلغاء المبلغ.
            لا يمكن اختيار شهر قبل <span className="lat">{matrix.earliestEditableMonth}</span>.
          </p>
          {errors.length > 0 && (
            <ul className="error" style={{ color: "var(--color-danger-dark)", margin: "10px 0 0", paddingInlineStart: 18 }}>
              {errors.map((e, i) => <li key={i}>{e}</li>)}
            </ul>
          )}
        </div>
      )}

      {matrix && (
        <div className="table-wrap">
          <table className="data">
            <thead>
              <tr>
                <th>المسمى الوظيفي</th>
                {cols.map((pid) => <th key={pid} title={per(pid).description || ""}><span dir="auto">{per(pid).shortName || "P" + pid}</span></th>)}
              </tr>
            </thead>
            <tbody>
              {rows.map((pos) => (
                <tr key={pos}>
                  <td><span className="id-badge">{pos}</span> <span dir="auto">{posTitle(pos)}</span></td>
                  {cols.map((pid) => {
                    const key = cellKey(pos, pid);
                    const c = cells[key];
                    if (editing) {
                      const changed = changes().some((x) => x.positionId === pos && x.periodId === pid);
                      return (
                        <td key={pid}>
                          <input className="lat" type="number" min="0" step="0.01" value={draft[key] ?? ""}
                            onChange={(e) => setDraft({ ...draft, [key]: e.target.value })}
                            style={{ width: 92, borderColor: changed ? "var(--color-primary)" : undefined, fontWeight: changed ? 700 : 400 }} />
                        </td>
                      );
                    }
                    return (
                      <td key={pid} className="lat" style={{ cursor: "pointer" }} title="عرض سجل التعديلات"
                        onClick={() => setHistory({ positionId: pos, periodId: pid, title: `${posTitle(pos)} — ${per(pid).shortName || "P" + pid}` })}>
                        {c && c.monthlyAmount != null ? (
                          <React.Fragment>
                            <b>{SAR(c.monthlyAmount)}</b>
                            <div className="muted" style={{ fontSize: 11 }}>{SAR(c.dailyRate)} / يوم · منذ {c.effectiveFrom}</div>
                          </React.Fragment>
                        ) : <span className="chip chip-warn">لا يوجد مبلغ</span>}
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {editing && (
        <div className="pager">
          <button className="btn btn-primary" disabled={saving || pending === 0} onClick={save}>
            <i className={"ti " + (saving ? "ti-loader-2" : "ti-device-floppy")} style={saving ? { animation: "spin .7s linear infinite" } : null}></i>
            حفظ (<span className="lat">{pending}</span>)
          </button>
          <button className="btn btn-ghost" disabled={saving} onClick={cancelEdit}><i className="ti ti-x"></i>إلغاء</button>
        </div>
      )}

      {history && !editing && (
        <div style={{ marginTop: 16 }}>
          <RateHistory {...history} onClose={() => setHistory(null)} />
        </div>
      )}
    </div>
  );
}

function AbsenceRatesScreen() {
  const [tab, setTab] = useStateD("matrix");
  const [periods, setPeriods] = useStateD(null);
  const [positions, setPositions] = useStateD(null);
  const [grades, setGrades] = useStateD(null);
  const [positionId, setPositionId] = useStateD("");
  const [includeInactive, setIncludeInactive] = useStateD(false);
  const [posSearch, setPosSearch] = useStateD("");
  const [error, setError] = useStateD("");
  const [loading, setLoading] = useStateD(false);

  async function loadAll() {
    setLoading(true); setError("");
    try {
      const [p, pos, g] = await Promise.all([
        apiGet("/api/absence-rates/periods"),
        apiGet(`/api/absence-rates/positions?includeInactive=${includeInactive}`),
        apiGet("/api/absence-rates/grades" + (positionId ? `?positionId=${encodeURIComponent(positionId)}` : "")),
      ]);
      setPeriods(p || []); setPositions(pos || []); setGrades(g || []);
    } catch (e) {
      setError(e.message || "تعذّر جلب البيانات المرجعية.");
    } finally {
      setLoading(false);
    }
  }

  async function loadGrades(pid) {
    setPositionId(pid);
    try {
      const g = await apiGet("/api/absence-rates/grades" + (pid ? `?positionId=${encodeURIComponent(pid)}` : ""));
      setGrades(g || []);
    } catch (e) {
      toast(`تعذّر جلب الدرجات: ${e.message}`);
    }
  }

  async function reloadPositions(inactive) {
    setIncludeInactive(inactive);
    try {
      setPositions(await apiGet(`/api/absence-rates/positions?includeInactive=${inactive}`) || []);
    } catch (e) {
      toast(`تعذّر جلب المسميات: ${e.message}`);
    }
  }

  useEffectD(() => { loadAll(); }, []);

  const shownPositions = (positions || []).filter((p) =>
    !posSearch.trim()
    || (p.title || "").includes(posSearch.trim())
    || p.positionId.includes(posSearch.trim()));
  const posTitle = (id) => ((positions || []).find((p) => p.positionId === id) || {}).title || id;

  return (
    <React.Fragment>
      <PageHead title="مبالغ فترات الغياب"
        desc="المبلغ الشهري لكل مسمى وظيفي وفترة يُقسم على ٣٠ ليعطي خصم اليوم. يعدّلها مسؤولو الموارد البشرية فقط، وتسري من شهر محدد دون تغيير الأشهر السابقة.">
        <button className="btn btn-soft" disabled={loading} onClick={loadAll}>
          <i className={"ti " + (loading ? "ti-loader-2" : "ti-refresh")}
             style={loading ? { animation: "spin .7s linear infinite" } : null}></i>تحديث
        </button>
      </PageHead>

      {error && <div className="card"><p className="error" style={{ color: "var(--color-danger-dark)", margin: 0 }}>{error}</p></div>}

      <div className="card">
        <div className="tabs">
          {[["matrix", "مصفوفة المبالغ"], ["periods", "الفترات"], ["grades", "الدرجات"], ["positions", "المسميات الوظيفية"]].map(([id, label]) => (
            <button key={id} className={"tab" + (tab === id ? " active" : "")} onClick={() => setTab(id)}>{label}</button>
          ))}
        </div>

        {tab === "matrix" && <RateMatrix positions={positions} periods={periods} />}

        {tab === "periods" && (
          <div className="fade-in">
            {!periods ? <p className="muted">جارٍ التحميل...</p> : (
              <div className="table-wrap">
                <table className="data">
                  <thead><tr><th>رقم الفترة</th><th>الاسم</th><th>الوصف</th><th>الساعات المطلوبة</th><th>في تقرير الغياب</th></tr></thead>
                  <tbody>
                    {periods.map((p) => (
                      <tr key={p.periodId} className={p.included ? "row-success" : ""}>
                        <td className="lat">{p.periodId}</td>
                        <td dir="auto">{p.shortName || "—"}</td>
                        <td dir="auto">{p.description || <span className="muted">—</span>}</td>
                        <td className="lat">{p.requiredHours}</td>
                        <td>{p.included ? <span className="chip chip-success">مشمولة</span> : <span className="chip chip-muted">غير مشمولة</span>}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}

        {tab === "grades" && (
          <div className="fade-in">
            <div className="formrow" style={{ marginBottom: 14 }}>
              <div className="field">
                <label>المسمى الوظيفي</label>
                <select value={positionId} onChange={(e) => loadGrades(e.target.value)}>
                  <option value="">كل الدرجات</option>
                  {(positions || []).map((p) => <option key={p.positionId} value={p.positionId}>{p.title || p.positionId} ({p.positionId})</option>)}
                </select>
              </div>
              <span className="sub" style={{ alignSelf: "end", paddingBottom: 10 }}>
                <span className="lat">{(grades || []).length}</span> درجة{positionId ? ` — ${posTitle(positionId)}` : ""}
              </span>
            </div>
            {!grades ? <p className="muted">جارٍ التحميل...</p> : (
              <div className="table-wrap">
                <table className="data">
                  <thead><tr><th>الدرجة</th><th>الاسم المختصر</th><th>الوصف</th><th>المستوى</th><th>نوع الموظف</th><th>الموظفون</th></tr></thead>
                  <tbody>
                    {grades.map((g) => (
                      <tr key={g.grade}>
                        <td><span className="id-badge">{g.grade}</span></td>
                        <td dir="auto">{g.shortName || "—"}</td>
                        <td dir="auto">{g.description || <span className="muted">—</span>}</td>
                        <td className="lat">{g.levelNo || "—"}</td>
                        <td className="lat">{g.typeId || "—"}</td>
                        <td className="lat">{g.employeeCount}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}

        {tab === "positions" && (
          <div className="fade-in">
            <div className="formrow" style={{ marginBottom: 14 }}>
              <div className="field"><label>بحث</label><input value={posSearch} onChange={(e) => setPosSearch(e.target.value)} placeholder="المسمى أو الرقم" /></div>
              <label style={{ display: "flex", gap: 7, alignItems: "center", alignSelf: "end", paddingBottom: 10, fontSize: 13 }}>
                <input type="checkbox" checked={includeInactive} onChange={(e) => reloadPositions(e.target.checked)}
                  style={{ accentColor: "var(--color-primary)", width: 15, height: 15 }} />
                إظهار غير النشطة
              </label>
              <span className="sub" style={{ alignSelf: "end", paddingBottom: 10 }}>
                <span className="lat">{shownPositions.length}</span> مسمى
              </span>
            </div>
            {!positions ? <p className="muted">جارٍ التحميل...</p> : (
              <div className="table-wrap">
                <table className="data">
                  <thead><tr><th>الرقم</th><th>المسمى الوظيفي</th><th>الحالة</th><th>الموظفون</th><th>الدرجات المسموحة</th><th></th></tr></thead>
                  <tbody>
                    {shownPositions.map((p) => (
                      <tr key={p.positionId}>
                        <td><span className="id-badge">{p.positionId}</span></td>
                        <td dir="auto">{p.title || "—"}</td>
                        <td>{p.active ? <span className="chip chip-success">نشط</span> : <span className="chip chip-muted">غير نشط</span>}</td>
                        <td className="lat">{p.employeeCount}</td>
                        <td>{p.grades.length ? p.grades.map((g) => <span key={g} className="chip" style={{ marginInlineEnd: 4 }}>{g}</span>) : <span className="muted">—</span>}</td>
                        <td>
                          <button className="btn btn-ghost btn-sm" title="عرض درجات هذا المسمى"
                            onClick={() => { loadGrades(p.positionId); setTab("grades"); }}>
                            <i className="ti ti-arrow-left"></i>الدرجات
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}
      </div>
    </React.Fragment>
  );
}

Object.assign(window, { D33DeductionsScreen, AbsenceRatesScreen });
