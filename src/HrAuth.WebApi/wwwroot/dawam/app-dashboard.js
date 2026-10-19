/* ============================================================ DAWAM — Dashboard (HR overview)
   Ported from dashboard.jsx. The total-employee figure is live (from
   /api/employees?pageSize=1 → total); the remaining tiles (present today,
   late, missing, department breakdown, activity feed) have no live endpoint
   yet, so they keep the design's sample values. */

function Ring({ pct, label, sub }) {
  const r = 52, c = 2 * Math.PI * r, off = c * (1 - pct / 100);
  return (
    <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 6 }}>
      <div style={{ position: "relative", width: 132, height: 132 }}>
        <svg width="132" height="132" style={{ transform: "rotate(-90deg)" }}>
          <circle cx="66" cy="66" r={r} fill="none" stroke="var(--color-divider)" strokeWidth="11" />
          <circle cx="66" cy="66" r={r} fill="none" stroke="var(--color-primary)" strokeWidth="11"
            strokeLinecap="round" strokeDasharray={c} strokeDashoffset={off} />
        </svg>
        <div style={{ position: "absolute", inset: 0, display: "grid", placeItems: "center", textAlign: "center" }}>
          <div>
            <div className="lat" style={{ fontWeight: 800, fontSize: 30, color: "var(--color-primary)", lineHeight: 1 }}>{pct}%</div>
            <div style={{ fontSize: 11.5, color: "var(--color-text-60)", marginTop: 2, maxWidth: 90, lineHeight: 1.25 }}>{label}</div>
          </div>
        </div>
      </div>
      {sub && <div style={{ fontSize: 12.5, color: "var(--color-text-60)" }}>{sub}</div>}
    </div>
  );
}

function DeptBars() {
  const rows = [
    { name: "تقنية المعلومات", present: 3, total: 3 },
    { name: "التشغيل والصيانة", present: 3, total: 4 },
    { name: "المالية", present: 3, total: 3 },
    { name: "الشؤون الأكاديمية", present: 2, total: 3 },
    { name: "خدمة المستفيدين", present: 2, total: 3 },
    { name: "الموارد البشرية", present: 2, total: 2 },
    { name: "الأمن والسلامة", present: 2, total: 2 },
  ];
  return (
    <ul className="activity" style={{ gap: 0 }}>
      {rows.map((r) => {
        const pct = Math.round((r.present / r.total) * 100);
        const color = pct === 100 ? "var(--color-success)" : pct >= 75 ? "var(--color-primary)" : "var(--color-warning)";
        return (
          <li key={r.name} style={{ display: "block", padding: "11px 4px" }}>
            <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 7 }}>
              <span style={{ fontSize: 13.5, fontWeight: 700 }}>{r.name}</span>
              <span className="lat" style={{ fontSize: 13, color: "var(--color-text-60)" }}>{r.present}/{r.total}</span>
            </div>
            <div className="bar"><i style={{ width: pct + "%", background: color }}></i></div>
          </li>
        );
      })}
    </ul>
  );
}

function DashboardScreen({ onNav, user, employeeCount }) {
  const empNum = employeeCount == null ? "—" : employeeCount;
  const name = (user && (user.displayName || user.username)) || "بك";
  const activity = [
    { ic: "ti-upload", cls: "ic-primary", t: "تم رفع ملف بصمات جديد", s: "attendance_2026-06-07_09.dat · ٥٤ سطر", time: "اليوم 16:42" },
    { ic: "ti-alert-triangle", cls: "ic-danger", t: "٤ بصمات ناقصة تتطلب المراجعة", s: "ضمن آخر عملية تسوية", time: "اليوم 16:42" },
    { ic: "ti-clock-exclamation", cls: "ic-warning", t: "٦ حالات تأخير هذا الأسبوع", s: "بإجمالي ١٢٢ دقيقة", time: "أمس 15:10" },
    { ic: "ti-user-plus", cls: "ic-success", t: "تمت إضافة موظف جديد", s: "وليد سعيد القرني · التشغيل والصيانة", time: "أمس 09:24" },
  ];
  return (
    <React.Fragment>
      {/* row 1 */}
      <div style={{ display: "grid", gridTemplateColumns: "1.5fr 1fr 0.9fr", gap: 22 }} className="dash-row1">
        <div className="card" style={{ display: "flex", gap: 18, alignItems: "center", minHeight: 168 }}>
          <div className="hero-art" style={{ width: 120, height: 120, flex: "none", borderRadius: "var(--radius-md)", background: "var(--color-primary-10)", display: "grid", placeItems: "center" }}>
            <i className="ti ti-businessplan" style={{ fontSize: 56, color: "var(--color-primary)" }}></i>
          </div>
          <div style={{ textAlign: "start", flex: 1 }}>
            <h1 style={{ fontSize: 22, fontWeight: 700, margin: "0 0 5px" }} dir="auto">حياكم الله، {name}</h1>
            <div style={{ fontSize: 13, color: "var(--color-text-60)", marginBottom: 16 }}>الأربعاء · ٢٤ ذو القعدة ١٤٤٧ هـ</div>
            <div className="hero-stats" style={{ display: "flex", gap: 28 }}>
              {[[empNum, "إجمالي الموظفين"], ["١٧", "حاضر اليوم"], ["٤", "تنبيهات مفتوحة"]].map(([n, l]) => (
                <div key={l}>
                  <div className="lat" style={{ fontWeight: 700, fontSize: 24, color: "var(--color-primary)" }}>{n}</div>
                  <div style={{ fontSize: 12, color: "var(--color-text-60)" }}>{l}</div>
                </div>
              ))}
            </div>
          </div>
        </div>

        <div className="card">
          <div className="card-head" style={{ marginBottom: 14 }}><h2>إجراءات سريعة</h2></div>
          <div className="quick-actions" style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
            <button className="btn btn-accent" style={{ justifyContent: "center", padding: "13px" }} onClick={() => onNav("upload")}><i className="ti ti-upload"></i>رفع البصمات</button>
            <button className="btn btn-primary" style={{ justifyContent: "center", padding: "13px" }} onClick={() => onNav("reconciliation")}><i className="ti ti-report-analytics"></i>تسوية الحضور</button>
            <button className="btn btn-ghost" style={{ justifyContent: "center", padding: "13px" }} onClick={() => onNav("employees")}><i className="ti ti-users"></i>الموظفون</button>
            <button className="btn btn-ghost" style={{ justifyContent: "center", padding: "13px" }} onClick={() => onNav("roster")}><i className="ti ti-calendar-time"></i>الجداول</button>
          </div>
        </div>

        <div className="card" style={{ display: "grid", placeItems: "center" }}>
          <Ring pct={85} label="حضور اليوم" sub={empNum === "—" ? "حضور اليوم" : `١٧ من ${empNum} موظفاً`} />
        </div>
      </div>

      {/* row 2 — stat tiles */}
      <div className="stat-row" style={{ gridTemplateColumns: "repeat(4, 1fr)" }}>
        {[
          { num: empNum, lbl: "إجمالي الموظفين", ic: "ti-users", cls: "ic-primary", delta: "في ٧ أقسام", dc: "var(--color-text-60)" },
          { num: "17", lbl: "الحاضرون اليوم", ic: "ti-user-check", cls: "ic-success", delta: "▲ 85% انضباط", dc: "var(--color-success-dark)" },
          { num: "6", lbl: "حالات تأخير (الأسبوع)", ic: "ti-clock-exclamation", cls: "ic-warning", delta: "122 دقيقة", dc: "rgb(183,110,30)" },
          { num: "4", lbl: "بصمات ناقصة للمراجعة", ic: "ti-alert-triangle", cls: "ic-danger", delta: "تتطلب إجراء", dc: "var(--color-danger-dark)" },
        ].map((t) => (
          <div className="stat-tile" key={t.lbl}>
            <div className={"ic " + t.cls}><i className={"ti " + t.ic}></i></div>
            <div className="meta">
              <div className="num">{t.num}</div>
              <div className="lbl">{t.lbl}</div>
              <div className="delta" style={{ color: t.dc }}>{t.delta}</div>
            </div>
          </div>
        ))}
      </div>

      {/* row 3 */}
      <div style={{ display: "grid", gridTemplateColumns: "1.2fr 1fr", gap: 22 }} className="dash-row3">
        <div className="card">
          <div className="card-head">
            <h2>الحضور حسب القسم — اليوم</h2>
            <span className="sub">٢٤ ذو القعدة ١٤٤٧</span>
          </div>
          <DeptBars />
        </div>
        <div className="card">
          <div className="card-head">
            <h2>أحدث النشاطات</h2>
            <button className="btn btn-soft btn-sm" onClick={() => onNav("reconciliation")}><i className="ti ti-arrow-left"></i>عرض التسوية</button>
          </div>
          <ul className="activity">
            {activity.map((a, i) => (
              <li key={i}>
                <div className={"ic " + a.cls}><i className={"ti " + a.ic}></i></div>
                <div className="body">
                  <div className="t">{a.t}</div>
                  <div className="s">{a.s}</div>
                  <div className="time">{a.time}</div>
                </div>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </React.Fragment>
  );
}

Object.assign(window, { DashboardScreen });
