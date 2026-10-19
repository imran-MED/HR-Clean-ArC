/* ============================================================ DAWAM — Reconciliation (interactive)
   Ported from reconciliation.jsx. Reads the live reconciliation result cached
   by the Upload screen (sessionStorage "hrauth.reconciliation"), whose `days[]`
   shape already matches the prototype's mock exactly. */
const { useState: useStateR } = React;

const FLAG_META = [
  { key: "late",     label: "متأخر",        cls: "warn" },
  { key: "early",    label: "انصراف مبكر",  cls: "warn" },
  { key: "missing",  label: "بصمة ناقصة",   cls: "bad"  },
  { key: "leave",    label: "إجازة",        cls: "ok"   },
  { key: "clean",    label: "منضبط",        cls: "ok"   },
  { key: "noroster", label: "بدون وردية",   cls: "mute" },
];

function dayHasFlag(d, flag) {
  switch (flag) {
    case "late":     return d.hasRoster && d.lateMinutes > 0;
    case "early":    return d.hasRoster && d.earlyDepartureMinutes > 0;
    case "missing":  return d.hasRoster && d.missingCount > 0;
    case "leave":    return !!d.onLeave;
    case "noroster": return !d.hasRoster && !d.onLeave;
    case "clean":    return d.hasRoster && d.missingCount === 0 && d.lateMinutes === 0 && d.earlyDepartureMinutes === 0;
    default: return false;
  }
}
const defaultFlags = () => ({ late: true, early: true, missing: true, leave: true, clean: true, noroster: true });
const rowKey = (d) => `${d.employeeId}|${d.date}`;

function periodPills(d) {
  if (d.onLeave) return <span className="pill pill-ok"><i className="ti ti-beach" style={{ marginInlineStart: 4, verticalAlign: "-2px" }}></i>إجازة{d.leaveName ? ` — ${d.leaveName}` : ""}</span>;
  if (!d.hasRoster) return <span className="muted">لا توجد وردية مسجّلة</span>;
  return d.periods.map((p, i) => {
    const time = <span className="lat" style={{ opacity: .7 }}>{p.startTime}–{p.endTime}</span>;
    if (p.isMissing)
      return <span key={i} className="pill pill-bad">{p.name} {time} ✗ ناقصة</span>;
    const flags = [];
    if (p.isLate) flags.push(`تأخير ${p.lateMinutes}د`);
    if (p.isEarlyDeparture) flags.push(`انصراف ${p.earlyDepartureMinutes}د`);
    if (flags.length === 0)
      return <span key={i} className="pill pill-ok">{p.name} {time} ✓</span>;
    return <span key={i} className="pill pill-warn">{p.name} {time} · {flags.join(" · ")}</span>;
  });
}

function StatTiles({ data, applied, onShortcut }) {
  const isFresh = applied.search === "" && !applied.from && !applied.to && !applied.position;
  const checked = FLAG_META.map((f) => f.key).filter((k) => applied.flags[k]);
  const solo = isFresh && checked.length === 1 ? checked[0] : null;
  const isAll = isFresh && checked.length === FLAG_META.length;
  const tiles = [
    { stat: "all",      num: data.days.length,                  lbl: "سجل دوام",        ic: "ti-calendar-stats", cls: "ic-primary" },
    { stat: "missing",  num: data.totalMissingPeriods,          lbl: "بصمات ناقصة",      ic: "ti-alert-triangle", cls: "ic-danger" },
    { stat: "late",     num: data.totalLateMinutes,             lbl: "دقائق التأخير",    ic: "ti-clock-exclamation", cls: "ic-warning" },
    { stat: "early",    num: data.totalEarlyDepartureMinutes,   lbl: "دقائق الانصراف المبكر", ic: "ti-door-exit", cls: "ic-warning" },
    { stat: "leave",    num: data.totalOnLeave || 0,            lbl: "أيام إجازة",       ic: "ti-beach",          cls: "ic-success" },
    { stat: "noroster", num: data.employeesWithoutRoster,       lbl: "بدون وردية",       ic: "ti-calendar-off",   cls: "ic-info" },
  ];
  return (
    <div className="stat-row" style={{ gridTemplateColumns: "repeat(6, 1fr)" }}>
      {tiles.map((t) => {
        const active = (t.stat === "all" && isAll) || t.stat === solo;
        return (
          <button key={t.stat} className={"stat-tile" + (active ? " active" : "")} onClick={() => onShortcut(t.stat)}>
            <div className={"ic " + t.cls}><i className={"ti " + t.ic}></i></div>
            <div className="meta">
              <div className="num">{t.num}</div>
              <div className="lbl">{t.lbl}</div>
            </div>
          </button>
        );
      })}
    </div>
  );
}

function Row({ d, open, onToggle }) {
  const rowCls = !d.hasRoster ? "" :
    d.missingCount > 0 ? "row-danger" :
    (d.lateMinutes > 0 || d.earlyDepartureMinutes > 0) ? "row-warning" : "row-success";
  return (
    <React.Fragment>
      <tr className={"clickable " + rowCls + (open ? " open" : "")} onClick={onToggle}>
        <td><span className="chev">{open ? "▾" : "▸"}</span></td>
        <td><span className="id-badge">{d.employeeId}</span></td>
        <td>
          <div className="emp-cell">
            <div className="av" style={{ background: avColor(d.employeeId) }}>{firstLetter(d.employeeName)}</div>
            <span dir="auto">{d.employeeName}</span>
          </div>
        </td>
        <td dir="auto">{d.position || <span className="muted">—</span>}</td>
        <td className="lat">{d.date}</td>
        <td className="muted">{dayNameAr(d.date)}</td>
        <td>{d.shiftName || <span className="muted">—</span>}</td>
        <td className="periods-cell">{periodPills(d)}</td>
        <td className="lat" style={{ textAlign: "center" }}>{d.punchCount}</td>
      </tr>
      {open && (
        <tr className="detail">
          <td colSpan={9}><DetailBlock d={d} /></td>
        </tr>
      )}
    </React.Fragment>
  );
}

function DetailBlock({ d }) {
  return (
    <div>
      <div className="detail-head">
        <div>
          <div className="detail-title">{longDateAr(d.date)}</div>
          <div className="detail-sub">
            <span className="lat">{d.employeeId}</span> · {d.employeeName}
            {" "}· الوردية: {d.shiftName || "—"} · <span className="lat">{d.punchCount}</span> بصمة
          </div>
        </div>
        <button className="btn btn-ghost btn-sm"><i className="ti ti-user"></i>ملف الموظف</button>
      </div>
      {d.onLeave ? (
        <p className="muted" style={{ marginTop: 12 }}><i className="ti ti-beach" style={{ marginInlineStart: 4 }}></i> الموظف في إجازة{d.leaveName ? ` (${d.leaveName})` : ""} في هذا التاريخ — لا تُحتسب بصمات ناقصة.</p>
      ) : !d.hasRoster ? (
        <p className="muted" style={{ marginTop: 12 }}>لا توجد وردية مجدولة في هذا التاريخ — لم تُطابق البصمات مع أي جدول دوام.</p>
      ) : (
        <table className="inner">
          <thead><tr>
            <th>الفترة</th><th>المجدول</th><th>أول بصمة</th><th>آخر بصمة</th><th>عدد البصمات</th><th>الحالة</th>
          </tr></thead>
          <tbody>
            {d.periods.map((p, i) => {
              let status, cls;
              if (p.isMissing) { status = "بصمة ناقصة — لا بصمة ضمن ±١٥ دقيقة"; cls = "pill-bad"; }
              else if (p.isLate && p.isEarlyDeparture) { status = `تأخير ${p.lateMinutes}د + انصراف ${p.earlyDepartureMinutes}د`; cls = "pill-warn"; }
              else if (p.isLate) { status = `تأخير ${p.lateMinutes} دقيقة`; cls = "pill-warn"; }
              else if (p.isEarlyDeparture) { status = `انصراف مبكر ${p.earlyDepartureMinutes} دقيقة`; cls = "pill-warn"; }
              else { status = "في الوقت المحدد"; cls = "pill-ok"; }
              return (
                <tr key={i}>
                  <td>
                    {p.name}
                    {p.breakups && p.breakups.length > 1 && (
                      <div className="lat muted small" style={{ marginTop: 3 }}>
                        {p.breakups.map((b) => `${b.name} ${b.startTime}–${b.endTime}`).join(" · ")}
                      </div>
                    )}
                  </td>
                  <td className="lat">{p.startTime}–{p.endTime}</td>
                  <td className="lat">{p.firstPunch || "—"}</td>
                  <td className="lat">{p.lastPunch || "—"}</td>
                  <td className="lat" style={{ textAlign: "center" }}>{p.punchCount}</td>
                  <td>
                    <span className={"pill " + cls}>{status}</span>
                    {p.hasDirectionIssue && (
                      <div className="small" style={{ marginTop: 4, color: "var(--color-warning-dark, #8a6d00)" }}>
                        <i className="ti ti-alert-triangle" style={{ verticalAlign: "-2px", marginInlineStart: 3 }}></i>
                        {" "}{p.directionNote} — صحّحها في شاشة «رفع البصمات».
                      </div>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </div>
  );
}

function ReconEmpty({ onNav }) {
  return (
    <React.Fragment>
      <PageHead title="تسوية الحضور" desc="مطابقة بصمات الحضور مع جداول الدوام — البصمات الناقصة والتأخير والانصراف المبكر." />
      <div className="card" style={{ textAlign: "center", padding: 48 }}>
        <div style={{ width: 56, height: 56, margin: "0 auto 14px", borderRadius: "var(--radius-md)", background: "var(--color-primary-10)", color: "var(--color-primary)", display: "grid", placeItems: "center" }}>
          <i className="ti ti-report-analytics" style={{ fontSize: 28 }}></i>
        </div>
        <div style={{ fontWeight: 700, fontSize: 16, marginBottom: 6 }}>لا توجد نتيجة تسوية بعد</div>
        <p className="muted" style={{ fontSize: 13.5, margin: "0 0 16px" }}>ارفع ملف بصمات أجهزة الحضور لعرض تسوية الدوام.</p>
        <button className="btn btn-primary" style={{ margin: "0 auto" }} onClick={() => onNav("upload")}><i className="ti ti-upload"></i>رفع البصمات</button>
      </div>
    </React.Fragment>
  );
}

function ReconciliationScreen({ onNav }) {
  const [data, setData] = useStateR(getRecon());
  const [refreshing, setRefreshing] = useStateR(false);
  const [pending, setPending] = useStateR({ search: "", from: "", to: "", position: "", flags: defaultFlags() });
  const [applied, setApplied] = useStateR({ search: "", from: "", to: "", position: "", flags: defaultFlags() });
  const [expanded, setExpanded] = useStateR(() => new Set());

  // Recompute the cached punches with the current server logic (joined shifts,
  // missing-checkout, etc.) and refresh in place — no need to re-upload.
  async function refresh() {
    if (!data || !Array.isArray(data.records) || !data.records.length) return;
    setRefreshing(true);
    try {
      const text = data.records
        .filter((r) => r.employeeId && r.timestamp)
        .map((r) => {
          const ts = String(r.timestamp);
          let time = ts.replace("T", " ").slice(11, 19);
          if (time.length === 5) time += ":00";
          const id = String(r.employeeId).trim().padStart(8, "0");
          return [id, ts.slice(0, 10), time, r.status, r.verify, r.workCode, r.reserved].join(" ");
        })
        .join("\n");
      const res = await fetch("/api/attendance/reconcile", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ fileName: data.fileName || "corrected.txt", content: text }),
      });
      const nd = await res.json().catch(() => null);
      if (res.ok && nd) { setRecon(nd); setData(nd); }
    } finally {
      setRefreshing(false);
    }
  }

  if (!data || !Array.isArray(data.days)) return <ReconEmpty onNav={onNav} />;

  // Distinct positions present in this result, for the position filter.
  const positions = Array.from(new Set(data.days.map((d) => d.position).filter(Boolean))).sort();

  const dirty = JSON.stringify(pending) !== JSON.stringify(applied);

  function commit(next) {
    setApplied(JSON.parse(JSON.stringify(next)));
    setPending(JSON.parse(JSON.stringify(next)));
  }
  function shortcut(stat) {
    const next = { search: "", from: "", to: "", position: "", flags: defaultFlags() };
    if (stat !== "all") { Object.keys(next.flags).forEach((k) => (next.flags[k] = false)); next.flags[stat] = true; }
    setExpanded(new Set());
    commit(next);
  }
  function toggleExpand(key) {
    setExpanded((s) => { const n = new Set(s); n.has(key) ? n.delete(key) : n.add(key); return n; });
  }

  const flagList = FLAG_META.map((f) => f.key);
  const anyChecked = flagList.some((f) => applied.flags[f]);
  const q = applied.search.trim();
  const filtered = data.days.filter((d) => {
    if (q) { const hay = `${d.employeeId} ${d.employeeName || ""}`; if (!hay.includes(q)) return false; }
    if (applied.position && d.position !== applied.position) return false;
    if (applied.from && d.date < applied.from) return false;
    if (applied.to && d.date > applied.to) return false;
    if (!anyChecked) return false;
    return flagList.some((f) => applied.flags[f] && dayHasFlag(d, f));
  });

  const allClean = data.totalMissingPeriods === 0 && data.totalLateMinutes === 0 && data.totalEarlyDepartureMinutes === 0;

  return (
    <React.Fragment>
      <div className="page-head">
        <div className="h">
          <h1>تسوية الحضور</h1>
          <p>مطابقة بصمات الحضور مع جداول الدوام — البصمات الناقصة والتأخير والانصراف المبكر. اضغط أي بطاقة أو صف للتفاصيل.</p>
        </div>
        <div className="tools">
          <button className="btn btn-ghost" onClick={refresh} disabled={refreshing} title="إعادة حساب البصمات الحالية بأحدث القواعد">
            <i className={"ti " + (refreshing ? "ti-loader-2" : "ti-refresh")} style={refreshing ? { animation: "spin .7s linear infinite" } : null}></i>إعادة الحساب
          </button>
          <button className="btn btn-ghost"><i className="ti ti-file-export"></i>تصدير Excel</button>
          <button className="btn btn-primary"><i className="ti ti-printer"></i>طباعة التقرير</button>
        </div>
      </div>

      <StatTiles data={data} applied={applied} onShortcut={shortcut} />

      <div className="card">
        <div className="card-head">
          <h2>نتائج التسوية</h2>
          <span className="sub">
            ملف <span className="mono">{data.fileName}</span>
            {data.totalLines != null && <React.Fragment> · <span className="lat">{data.importedCount}</span>/<span className="lat">{data.totalLines}</span> بصمة</React.Fragment>}
          </span>
        </div>

        <div className="filter-panel">
          <div className="filter-head">
            <div className="filter-title">
              <div className="ic"><i className="ti ti-filter"></i></div>
              <span>التصفية</span>
              {dirty && <span className="dirty-dot" title="تغييرات غير مطبّقة"></span>}
            </div>
            <span className="sub">عرض <span className="lat">{filtered.length}</span> من <span className="lat">{data.days.length}</span> سجل</span>
          </div>

          <div className="filter-grid">
            <div className="field">
              <label>الموظف</label>
              <div className="in-icon">
                <i className="ti ti-search"></i>
                <input value={pending.search} placeholder="ابحث بالرقم الوظيفي أو الاسم"
                  onChange={(e) => setPending({ ...pending, search: e.target.value })}
                  onKeyDown={(e) => e.key === "Enter" && commit(pending)} />
              </div>
            </div>
            <div className="field">
              <label>المنصب</label>
              <select value={pending.position} onChange={(e) => setPending({ ...pending, position: e.target.value })}>
                <option value="">كل المناصب</option>
                {positions.map((p) => <option key={p} value={p}>{p}</option>)}
              </select>
            </div>
            <div className="field">
              <label>من تاريخ</label>
              <input type="date" value={pending.from} onChange={(e) => setPending({ ...pending, from: e.target.value })} />
            </div>
            <div className="field">
              <label>إلى تاريخ</label>
              <input type="date" value={pending.to} onChange={(e) => setPending({ ...pending, to: e.target.value })} />
            </div>
          </div>

          <div className="filter-flags">
            <span className="lab">الحالة</span>
            <div className="flagchips">
              {FLAG_META.map((f) => {
                const on = pending.flags[f.key];
                return (
                  <button key={f.key} className={"flagchip " + f.cls + (on ? " on" : "")}
                    onClick={() => setPending({ ...pending, flags: { ...pending.flags, [f.key]: !on } })}>
                    <span className="d"></span><span>{f.label}</span>
                  </button>
                );
              })}
            </div>
          </div>

          <div className="filter-actions">
            <button className={"btn btn-primary" + (dirty ? " dirty" : "")} id="apply" onClick={() => commit(pending)}>
              <i className="ti ti-check"></i>تطبيق التصفية
            </button>
            <button className="btn btn-ghost" onClick={() => { setExpanded(new Set()); commit({ search: "", from: "", to: "", position: "", flags: defaultFlags() }); }}>
              <i className="ti ti-rotate"></i>إعادة تعيين
            </button>
          </div>
        </div>

        {allClean && (
          <div className="chip chip-success" style={{ margin: "14px 0 0", padding: "8px 14px" }}>
            <span className="dot"></span>جميع الفترات المجدولة تم حضورها في الوقت المحدد.
          </div>
        )}

        <div className="table-wrap" style={{ marginTop: 16 }}>
          <table className="data">
            <thead><tr>
              <th></th><th>الرقم الوظيفي</th><th>الموظف</th><th>المنصب</th><th>التاريخ</th><th>اليوم</th>
              <th>الوردية</th><th>الفترات المجدولة — ✓ منضبط / ✗ ناقصة · تأخير</th><th style={{ textAlign: "center" }}>البصمات</th>
            </tr></thead>
            <tbody>
              {filtered.length === 0 ? (
                <tr><td colSpan={9} className="empty-row">لا توجد سجلات مطابقة لمعايير التصفية الحالية.</td></tr>
              ) : (
                filtered.map((d) => (
                  <Row key={rowKey(d)} d={d} open={expanded.has(rowKey(d))} onToggle={() => toggleExpand(rowKey(d))} />
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </React.Fragment>
  );
}

Object.assign(window, { ReconciliationScreen });
