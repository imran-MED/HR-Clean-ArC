/* ============================================================ DAWAM — app shell */

const NAV = [
  { group: "الرئيسية", items: [
    { id: "dashboard", label: "لوحة المعلومات", icon: "ti-layout-dashboard" },
  ]},
  { group: "الحضور والدوام", items: [
    { id: "reconciliation", label: "تسوية الحضور", icon: "ti-report-analytics", countKey: "reconciliation" },
    { id: "upload", label: "رفع البصمات", icon: "ti-upload" },
    { id: "roster", label: "جداول الدوام", icon: "ti-calendar-time" },
    { id: "absent", label: "تقرير الغياب", icon: "ti-user-x" },
    { id: "missing", label: "البصمات الناقصة", icon: "ti-fingerprint-off" },
  ]},
  { group: "الإجازات", items: [
    { id: "leaveApply", label: "طلب إجازة", icon: "ti-beach" },
    { id: "leaveApprovals", label: "اعتماد الإجازات", icon: "ti-checklist", countKey: "leaveApprovals" },
  ]},
  { group: "الرواتب والخصومات", items: [
    { id: "d33", label: "خصومات الغياب (D33)", icon: "ti-receipt-2" },
    { id: "absenceRates", label: "مبالغ فترات الغياب", icon: "ti-coin" },
    { id: "lateEarly", label: "التأخر والخروج المبكر", icon: "ti-clock-exclamation" },
    { id: "rules", label: "قواعد الاحتساب", icon: "ti-adjustments" },
  ]},
  { group: "إدارة الموظفين", items: [
    { id: "employees", label: "الموظفون", icon: "ti-users", countKey: "employees" },
    { id: "roles", label: "الصلاحيات والأدوار", icon: "ti-shield-lock" },
  ]},
];

const TITLES = {
  dashboard: "لوحة المعلومات",
  reconciliation: "تسوية الحضور",
  upload: "رفع البصمات",
  roster: "جداول الدوام",
  absent: "تقرير الغياب",
  missing: "البصمات الناقصة",
  leaveApply: "طلب إجازة",
  leaveApprovals: "اعتماد الإجازات",
  d33: "خصومات الغياب (D33)",
  absenceRates: "مبالغ فترات الغياب",
  lateEarly: "التأخر والخروج المبكر",
  rules: "قواعد الاحتساب",
  employees: "الموظفون",
  roles: "الصلاحيات والأدوار",
};
const CRUMBS = {
  dashboard: ["الرئيسية"],
  reconciliation: ["الحضور والدوام", "تسوية الحضور"],
  upload: ["الحضور والدوام", "رفع البصمات"],
  roster: ["الحضور والدوام", "جداول الدوام"],
  absent: ["الحضور والدوام", "تقرير الغياب"],
  missing: ["الحضور والدوام", "البصمات الناقصة"],
  leaveApply: ["الإجازات", "طلب إجازة"],
  leaveApprovals: ["الإجازات", "اعتماد الإجازات"],
  d33: ["الرواتب والخصومات", "خصومات الغياب (D33)"],
  absenceRates: ["الرواتب والخصومات", "مبالغ فترات الغياب"],
  lateEarly: ["الرواتب والخصومات", "التأخر والخروج المبكر"],
  rules: ["الرواتب والخصومات", "قواعد الاحتساب"],
  employees: ["إدارة الموظفين", "الموظفون"],
  roles: ["إدارة الموظفين", "الصلاحيات والأدوار"],
};

/* The signed-in identity comes from the login session, with sensible Arabic
   fallbacks so the footer never renders blank. */
function sessionName(user) {
  if (!user) return "مستخدم";
  return user.displayName || user.username || user.userId || "مستخدم";
}
function sessionRole(user) {
  const roles = (user && user.roles) || [];
  if (roles.length) return roles[0];
  return "موظف";
}

/* ---- responsive navigation ------------------------------------------------
   • ≥ 1024px: the sidebar is docked. The menu button collapses it to an icon
     rail (remembered per browser).
   • < 1024px: the sidebar becomes a drawer that slides in from the reading
     side (right in Arabic, left in English) over a dimmed backdrop. It closes
     on navigation, backdrop click, the close button or Escape. */
const NAV_RAIL_KEY = "dawam.navRail";
const DRAWER_MQ = "(max-width: 1023.98px)";

function useIsDrawer() {
  const mq = React.useMemo(() => window.matchMedia(DRAWER_MQ), []);
  const [isDrawer, setIsDrawer] = React.useState(mq.matches);
  React.useEffect(() => {
    const on = (e) => setIsDrawer(e.matches);
    mq.addEventListener ? mq.addEventListener("change", on) : mq.addListener(on);
    return () => (mq.removeEventListener ? mq.removeEventListener("change", on) : mq.removeListener(on));
  }, [mq]);
  return isDrawer;
}

function Sidebar({ active, onNav, onLogout, user, counts, open, rail, isDrawer, onClose }) {
  const cls = "side" + (isDrawer ? " drawer" : "") + (isDrawer && open ? " open" : "") + (!isDrawer && rail ? " rail" : "");
  return (
    <aside className={cls} id="dawam-nav" aria-label="القائمة الرئيسية"
      aria-hidden={isDrawer && !open ? "true" : undefined}>
      <div className="logo">
        <div className="mark">د</div>
        <div className="name">Dawam<small>نظام الحضور والدوام</small></div>
        {isDrawer && (
          <button className="iconbtn side-close" title="إغلاق القائمة" aria-label="إغلاق القائمة" onClick={onClose}
            tabIndex={open ? 0 : -1}>
            <i className="ti ti-x"></i>
          </button>
        )}
      </div>
      <nav>
        {NAV.map((sec) => (
          <React.Fragment key={sec.group}>
            <div className="group">{sec.group}</div>
            {sec.items.map((it) => {
              const count = it.countKey ? counts[it.countKey] : null;
              return (
                <button
                  key={it.id}
                  className={"nav-item" + (active === it.id ? " active" : "")}
                  aria-current={active === it.id ? "page" : undefined}
                  title={!isDrawer && rail ? it.label : undefined}
                  tabIndex={isDrawer && !open ? -1 : 0}
                  onClick={() => onNav(it.id)}
                >
                  <i className={"ti " + it.icon}></i>
                  <span className="lbl">{it.label}</span>
                  {count != null && <span className="count lat">{count}</span>}
                </button>
              );
            })}
          </React.Fragment>
        ))}
      </nav>
      <div className="side-foot">
        <div className="who">
          <div className="avatar"><img src="dawam/user-avatar.png" alt="" /></div>
          <div className="txt">
            <div className="nm">{sessionName(user)}</div>
            <div className="rl">{sessionRole(user)}</div>
          </div>
          <button className="logout" title="تسجيل الخروج" aria-label="تسجيل الخروج" onClick={onLogout}
            tabIndex={isDrawer && !open ? -1 : 0}>
            <i className="ti ti-logout"></i>
          </button>
        </div>
      </div>
    </aside>
  );
}

function Topbar({ title, onMenu, menuOpen, isDrawer }) {
  return (
    <header className="topbar">
      <button className="iconbtn menu-btn" onClick={onMenu}
        aria-controls="dawam-nav" aria-expanded={isDrawer ? !!menuOpen : undefined}
        title={isDrawer ? "القائمة" : "طي القائمة / توسيعها"} aria-label="القائمة">
        <i className="ti ti-menu-2"></i>
      </button>
      <div className="pagetitle">{title}</div>
      <div className="search">
        <i className="ti ti-search"></i>
        <input placeholder="ابحث عن موظف أو رقم وظيفي..." />
      </div>
      <div className="spacer"></div>
      <button className="iconbtn lang-btn" title="اللغة"
        onClick={() => window.dawamToggleLang && window.dawamToggleLang()}>
        <i className="ti ti-language"></i>
        <span className="lat">{window.DAWAM_LANG === "en" ? "ع" : "EN"}</span>
      </button>
      <button className="iconbtn" title="الإشعارات"><span className="dot"></span><i className="ti ti-bell"></i></button>
      <button className="iconbtn hide-sm" title="المساعدة"><i className="ti ti-help"></i></button>
    </header>
  );
}

function Breadcrumb({ screen }) {
  const crumbs = CRUMBS[screen] || [];
  return (
    <div className="breadcrumb">
      <span>الرئيسية</span>
      {crumbs.map((c, i) => (
        <React.Fragment key={c}>
          <i className="ti ti-chevron-left bc-sep"></i>
          <span className={i === crumbs.length - 1 ? "here" : ""}>{c}</span>
        </React.Fragment>
      ))}
    </div>
  );
}

function AppShell({ active, onNav, onLogout, user, counts, children }) {
  const isDrawer = useIsDrawer();
  const [open, setOpen] = React.useState(false);
  const [rail, setRail] = React.useState(() => {
    try { return localStorage.getItem(NAV_RAIL_KEY) === "1"; } catch { return false; }
  });

  // Leaving drawer mode (e.g. rotating a tablet) must not leave it open.
  React.useEffect(() => { if (!isDrawer) setOpen(false); }, [isDrawer]);

  // While the drawer is open: Escape closes it and the page behind can't scroll.
  React.useEffect(() => {
    if (!open) return;
    const onKey = (e) => { if (e.key === "Escape") setOpen(false); };
    document.addEventListener("keydown", onKey);
    document.body.classList.add("no-scroll");
    return () => { document.removeEventListener("keydown", onKey); document.body.classList.remove("no-scroll"); };
  }, [open]);

  function toggleMenu() {
    if (isDrawer) { setOpen((o) => !o); return; }
    setRail((r) => {
      const n = !r;
      try { localStorage.setItem(NAV_RAIL_KEY, n ? "1" : "0"); } catch {}
      return n;
    });
  }
  function nav(id) { setOpen(false); onNav(id); }

  return (
    <div className={"app" + (!isDrawer && rail ? " is-rail" : "")}>
      <Sidebar active={active} onNav={nav} onLogout={onLogout} user={user} counts={counts}
        open={open} rail={rail} isDrawer={isDrawer} onClose={() => setOpen(false)} />
      {isDrawer && <div className={"side-backdrop" + (open ? " show" : "")} onClick={() => setOpen(false)} aria-hidden="true"></div>}
      <main>
        <Topbar title={TITLES[active]} onMenu={toggleMenu} menuOpen={open} isDrawer={isDrawer} />
        <div className="page fade-in" key={active}>
          <Breadcrumb screen={active} />
          {children}
        </div>
      </main>
    </div>
  );
}

/* Shared little page-header used by several screens (from dashboard.jsx). */
function PageHead({ title, desc, children }) {
  return (
    <div className="page-head">
      <div className="h">
        <h1>{title}</h1>
        {desc && <p>{desc}</p>}
      </div>
      {children && <div className="tools">{children}</div>}
    </div>
  );
}

Object.assign(window, { AppShell, PageHead, NAV, TITLES });
