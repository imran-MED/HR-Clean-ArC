/* ============================================================ DAWAM — root app
   Ported from main.jsx. The mock login is wired to POST /api/auth/login; the
   issued session is stored under "hrauth.session" (shared with the legacy
   pages) and gates the app shell. */
const { useState: useStateM, useEffect: useEffectM } = React;

/* Auto-logout: sign the user out after this many ms with no interaction. */
const IDLE_LIMIT_MS = 10 * 60 * 1000; // 10 minutes

function Login({ onSignIn }) {
  const [u, setU] = useStateM("");
  const [p, setP] = useStateM("");
  const [busy, setBusy] = useStateM(false);
  const [error, setError] = useStateM("");

  async function submit(e) {
    e.preventDefault();
    setBusy(true); setError("");
    try {
      const data = await apiSend("/api/auth/login", "POST", { userName: u.trim(), password: p });
      setSession(data);
      onSignIn(data);
    } catch (err) {
      setError(err.status === 401 ? "بيانات الدخول غير صحيحة." : (err.message || "تعذّر الاتصال بالخادم."));
      setBusy(false);
    }
  }

  return (
    <div style={{ minHeight: "100vh", display: "grid", gridTemplateColumns: "1.05fr 1fr", background: "var(--color-bg)" }} className="login-wrap">
      {/* form panel */}
      <div style={{ display: "grid", placeItems: "center", padding: 32 }}>
        <div style={{ width: "100%", maxWidth: 380 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 11, justifyContent: "flex-start", marginBottom: 30 }}>
            <div style={{ width: 44, height: 44, borderRadius: "var(--radius-lg)", background: "var(--color-primary)", display: "grid", placeItems: "center", color: "#fff", fontWeight: 700, fontSize: 22, boxShadow: "var(--shadow-primary)" }}>د</div>
            <div style={{ fontFamily: "var(--font-latin)", fontWeight: 700, fontSize: 26, letterSpacing: ".25px" }}>Dawam</div>
          </div>
          <h1 style={{ fontSize: 24, fontWeight: 700, margin: "0 0 6px", textAlign: "start" }}>تسجيل الدخول</h1>
          <p style={{ fontSize: 14, color: "var(--color-text-60)", margin: "0 0 26px", textAlign: "start" }}>أدخل بياناتك للوصول إلى نظام الحضور والدوام</p>
          <form onSubmit={submit} style={{ display: "flex", flexDirection: "column", gap: 16 }}>
            <div className="field">
              <label>اسم المستخدم</label>
              <div className="in-icon"><i className="ti ti-user"></i><input className="lat" value={u} onChange={(e) => setU(e.target.value)} placeholder="username" /></div>
            </div>
            <div className="field">
              <label>كلمة المرور</label>
              <div className="in-icon"><i className="ti ti-lock"></i><input type="password" value={p} onChange={(e) => setP(e.target.value)} placeholder="••••••••" /></div>
            </div>
            {error && <p className="error" style={{ color: "var(--color-danger-dark)", margin: 0, fontSize: 13 }}>{error}</p>}
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", fontSize: 13 }}>
              <label style={{ display: "flex", gap: 7, alignItems: "center", color: "var(--color-text-70)" }}>
                <input type="checkbox" defaultChecked style={{ accentColor: "var(--color-primary)", width: 15, height: 15 }} />تذكّرني
              </label>
              <a href="#" style={{ color: "var(--color-primary)", textDecoration: "none", fontWeight: 600 }}>نسيت كلمة المرور؟</a>
            </div>
            <button className="btn btn-primary" disabled={busy} style={{ justifyContent: "center", padding: "12px", fontSize: 15, marginTop: 4 }}>
              {busy ? <i className="ti ti-loader-2" style={{ animation: "spin .7s linear infinite" }}></i> : <i className="ti ti-login-2"></i>}
              {busy ? "جارٍ الدخول..." : "تسجيل الدخول"}
            </button>
          </form>
          <p style={{ fontSize: 12, color: "var(--color-text-50)", textAlign: "center", marginTop: 26 }}>
            موثّق عبر واجهة HrAuth · <span className="lat">hrapi.qm.edu.sa</span>
          </p>
        </div>
      </div>
      {/* brand panel */}
      <div style={{ background: "linear-gradient(160deg, var(--color-primary) 0%, var(--color-primary-dark) 100%)", position: "relative", overflow: "hidden", display: "grid", placeItems: "center", padding: 40 }}>
        <div style={{ position: "absolute", inset: 0, opacity: .08, backgroundImage: "radial-gradient(circle at 1px 1px, #fff 1.5px, transparent 0)", backgroundSize: "26px 26px" }}></div>
        <div style={{ position: "relative", textAlign: "center", color: "#fff", maxWidth: 380 }}>
          <div style={{ width: 84, height: 84, margin: "0 auto 24px", borderRadius: "var(--radius-xl)", background: "rgba(255,255,255,.12)", display: "grid", placeItems: "center", border: "1px solid rgba(255,255,255,.18)" }}>
            <i className="ti ti-fingerprint" style={{ fontSize: 44 }}></i>
          </div>
          <h2 style={{ fontSize: 26, fontWeight: 700, margin: "0 0 12px" }}>نظام الحضور والدوام</h2>
          <p style={{ fontSize: 15, lineHeight: 1.7, color: "rgba(255,255,255,.82)", margin: 0 }}>
            رفع بصمات أجهزة الحضور، ومطابقتها مع جداول الدوام، ورصد التأخير والانصراف المبكر والبصمات الناقصة — في منصّة واحدة.
          </p>
        </div>
      </div>
    </div>
  );
}

function App() {
  const [session, setSess] = useStateM(() => getSession());
  const authed = !!(session && session.accessToken);
  const [screen, setScreen] = useStateM(() => sessionStorage.getItem("dawam.screen") || "dashboard");
  const [empCount, setEmpCount] = useStateM(null);
  const [approvalCount, setApprovalCount] = useStateM(null);

  function nav(s) { setScreen(s); sessionStorage.setItem("dawam.screen", s); window.scrollTo(0, 0); }
  function signIn(data) { setSess(data); }
  function logout() { clearSession(); setSess(null); }

  // Idle auto-logout: any interaction resets a 10-minute timer; when it
  // elapses with no activity the session is cleared and the login screen
  // returns. Listeners are torn down on logout (when authed flips false).
  useEffectM(() => {
    if (!authed) return;
    let timer;
    const reset = () => {
      clearTimeout(timer);
      timer = setTimeout(() => { clearSession(); setSess(null); }, IDLE_LIMIT_MS);
    };
    const events = ["mousemove", "mousedown", "keydown", "scroll", "touchstart", "click"];
    events.forEach((e) => window.addEventListener(e, reset, { passive: true }));
    reset();
    return () => {
      clearTimeout(timer);
      events.forEach((e) => window.removeEventListener(e, reset));
    };
  }, [authed]);

  // Live employee count for the sidebar badge + dashboard hero.
  useEffectM(() => {
    if (!authed) return;
    apiGet("/api/employees?page=1&pageSize=1")
      .then((d) => setEmpCount(d.total))
      .catch(() => setEmpCount(null));
  }, [authed]);

  // Pending leave approvals for the sidebar badge (quiet on failure — most
  // employees have nothing to approve, and SmartHCM may be unreachable).
  useEffectM(() => {
    if (!authed) return;
    apiGet("/api/leave-requests/approvals")
      .then((d) => setApprovalCount(Array.isArray(d) && d.length ? d.length : null))
      .catch(() => setApprovalCount(null));
  }, [authed]);

  if (!authed) return <Login onSignIn={signIn} />;

  const recon = getRecon();
  const counts = {
    employees: empCount,
    reconciliation: recon && Array.isArray(recon.days) ? recon.days.length : null,
    leaveApprovals: approvalCount,
  };

  const SCREENS = {
    dashboard: <DashboardScreen onNav={nav} user={session} employeeCount={empCount} />,
    reconciliation: <ReconciliationScreen onNav={nav} />,
    upload: <UploadScreen onNav={nav} />,
    roster: <RosterScreen />,
    absent: <AbsentReportScreen />,
    missing: <MissingPunchScreen />,
    leaveApply: <LeaveApplyScreen />,
    leaveApprovals: <LeaveApprovalsScreen onCount={(n) => setApprovalCount(n || null)} />,
    d33: <D33DeductionsScreen />,
    absenceRates: <AbsenceRatesScreen />,
    lateEarly: <LateEarlyScreen />,
    rules: <AttendanceRulesScreen />,
    employees: <EmployeesScreen />,
    roles: <RolesScreen />,
  };

  return (
    <AppShell active={screen} onNav={nav} onLogout={logout} user={session} counts={counts}>
      {SCREENS[screen] || SCREENS.dashboard}
    </AppShell>
  );
}

ReactDOM.createRoot(document.getElementById("root")).render(<App />);
