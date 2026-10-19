/* ============================================================ DAWAM — i18n
   Lightweight Arabic⇄English layer. The components render Arabic (the source
   language); when English is selected this translates the rendered DOM in place
   and flips the document direction to LTR. Toggling reloads the page so the DOM
   resets to the Arabic source before (optionally) re-translating — no reverse
   dictionary needed. Language choice persists in localStorage.

   Loaded as a plain <script> before the Babel component scripts. The only hook
   the UI needs is window.dawamToggleLang() (wired to the topbar button) and
   window.DAWAM_LANG (the current language code). */
(function () {
  var LANG = (function () {
    try { return localStorage.getItem("dawam.lang") === "en" ? "en" : "ar"; }
    catch (e) { return "ar"; }
  })();

  window.DAWAM_LANG = LANG;
  window.dawamToggleLang = function () {
    try { localStorage.setItem("dawam.lang", LANG === "en" ? "ar" : "en"); } catch (e) {}
    window.location.reload();
  };

  // Direction + lang attribute apply immediately (root exists in <head>).
  document.documentElement.lang = LANG;
  document.documentElement.dir = LANG === "en" ? "ltr" : "rtl";

  if (LANG !== "en") return; // Arabic is the source — nothing to translate.

  /* ---- AR → EN dictionary (full UI phrases; applied longest-first as
          substrings so interpolated strings translate too). ---------------- */
  var DICT = {
    // Navigation (drawer / rail)
    "القائمة الرئيسية": "Main menu",
    "إغلاق القائمة": "Close menu",
    "طي القائمة / توسيعها": "Collapse / expand menu",
    "القائمة": "Menu",
    // Leaves (apply + approvals)
    "الإجازات": "Leaves",
    "طلب إجازة": "Apply for leave",
    "اعتماد الإجازات": "Leave approvals",
    "قدّم طلب إجازة إلى SmartHCM وتابع حالة طلباتك — بنفس طريقة تطبيق دوام. يُحتسب عدد الأيام تلقائياً مع مراعاة العطل.":
      "Submit a leave request to SmartHCM and track your requests — the same way as the Dawam app. The number of days is calculated automatically, taking holidays into account.",
    "طلب جديد": "New request",
    "نوع الإجازة": "Leave type",
    "— اختر —": "— Choose —",
    "من تاريخ": "From date",
    "إلى تاريخ": "To date",
    "عدد الأيام": "Number of days",
    "إجازة بالساعات": "Hourly leave",
    "من الساعة": "From time",
    "إلى الساعة": "To time",
    "أقصى مدة متصلة: ": "Longest continuous stretch: ",
    "الحد الأدنى: ": "Minimum: ",
    "سيعتمد الطلب: ": "Will be approved by: ",
    "ملاحظات (اختياري)": "Remarks (optional)",
    "المرفقات (تقرير طبي أو غيره — حتى ٥ ملفات، ١٠ ميجابايت لكل ملف)": "Attachments (medical report or other — up to 5 files, 10 MB each)",
    "إرفاق ملف": "Attach file",
    "سيُسجَّل الطلب بأنه مرفق به تقرير طبي.": "The request will be recorded as having a medical certificate attached.",
    "تقديم الطلب": "Submit request",
    "جارٍ الإرسال...": "Submitting...",
    "تم تقديم الطلب بنجاح.": "Request submitted successfully.",
    "تم تقديم طلب الإجازة.": "Leave request submitted.",
    "رفض SmartHCM الطلب:": "SmartHCM refused the request:",
    "طلباتي": "My requests",
    "قيد الانتظار": "Pending",
    "معتمدة": "Approved",
    "مرفوضة": "Rejected",
    "مسحوبة": "Withdrawn",
    "رقم الطلب": "Request no.",
    "الفترة": "Period",
    "المدة": "Duration",
    "لا توجد طلبات إجازة.": "No leave requests.",
    "سحب الطلب": "Withdraw request",
    "سحب": "Withdraw",
    "تم سحب الطلب.": "Request withdrawn.",
    "رفض SmartHCM سحب الطلب.": "SmartHCM refused to withdraw the request.",
    "تعذّر سحب الطلب.": "Could not withdraw the request.",
    "تعذّر جلب أنواع الإجازات.": "Could not load leave types.",
    "تعذّر جلب طلباتك.": "Could not load your requests.",
    "تعذّر احتساب عدد الأيام.": "Could not calculate the number of days.",
    "تعذّر تقديم الطلب.": "Could not submit the request.",
    "اختر نوع الإجازة.": "Choose a leave type.",
    "تاريخ النهاية يجب أن يكون في تاريخ البداية أو بعده.": "The end date must be on or after the start date.",
    "أدخل وقت البداية والنهاية للإجازة بالساعات.": "Enter the start and end time for an hourly leave.",
    "عدد الأيام يجب أن يكون أكبر من صفر.": "The number of days must be greater than zero.",
    "انتهت جلسة SmartHCM — يرجى تسجيل الخروج ثم الدخول مجدداً.": "Your SmartHCM session has ended — please sign out and sign in again.",
    "طلبات الإجازة المعلّقة لموظفيك في SmartHCM — اعتمدها أو ارفضها. بعض الأنواع تُحوَّل للموارد البشرية بعد موافقتك.":
      "Pending leave requests from your staff in SmartHCM — approve or reject them. Some leave types go on to HR after your approval.",
    "طلبات بانتظار اعتمادك": "Requests awaiting your approval",
    "لا توجد طلبات بانتظار اعتمادك.": "No requests awaiting your approval.",
    "تعذّر جلب طلبات الاعتماد.": "Could not load approval requests.",
    "يُحوَّل للموارد البشرية": "Goes on to HR",
    "موافقتك تحوّل الطلب للموارد البشرية للاعتماد النهائي": "Your approval forwards the request to HR for final approval",
    "تحويل للموارد البشرية": "Forward to HR",
    "تم تحويل الطلب للموارد البشرية.": "Request forwarded to HR.",
    "تحويل": "Forward",
    "اعتماد": "Approve",
    "إلغاء الطلب": "Cancel request",
    "تم اعتماد الطلب.": "Request approved.",
    "تم رفض الطلب.": "Request rejected.",
    "تم إلغاء الطلب.": "Request cancelled.",
    "رفض SmartHCM الإجراء.": "SmartHCM refused the action.",
    "تعذّر تنفيذ الإجراء.": "Could not perform the action.",
    "تعذّر جلب التفاصيل.": "Could not load the details.",
    "اكتب سبب الرفض.": "Enter the reason for rejection.",
    "سبب الرفض (مطلوب)": "Reason for rejection (required)",
    "تأكيد ": "Confirm ",
    "تراجع": "Back",
    "لا يوجد رقم طلب": "No request number",
    "لا توجد ملاحظات من الموظف.": "No remarks from the employee.",
    "التفاصيل": "Details",
    "الإجراء": "Action",
    "طلب": "request",
    "ساعة": "hour",
    "يوم": "day",
    "أيام": "days",
    "تحديث": "Refresh",
    // D33 deductions
    "الرواتب والخصومات": "Payroll & Deductions",
    "خصومات الغياب (D33)": "Absence Deductions (D33)",
    "غياب الهيئة التعليمية والإدارية لفترة الرواتب (١٦ من الشهر السابق حتى ١٥) مع مبلغ الخصم لكل فترة غياب. المصدر: SmartHCM عبر HR API.":
      "Teaching-staff / admin absences for the payroll period (16th of the previous month to the 15th) with the deduction amount per absent block. Source: SmartHCM via the HR API.",
    "فترة الرواتب": "Payroll period",
    "شهر الرواتب": "Pay month",
    "اختر شهر الرواتب.": "Choose the pay month.",
    "الجنس": "Gender",
    "رجال": "Men",
    "نساء": "Women",
    "عدد الصفوف": "Rows per page",
    "تضمين الأيام المُستنتَجة": "Include synthesized days",
    "جلب الخصومات": "Load deductions",
    "تعذّر جلب الخصومات.": "Could not load deductions.",
    "تصدير CSV": "Export CSV",
    "تم تصدير": "Exported",
    "فشل التصدير: ": "Export failed: ",
    "إجمالي خصم الشهر (ر.س)": "Month's total deduction (SAR)",
    "فترة غياب": "absent blocks",
    "مجموع هذه الصفحة (ر.س)": "This page's total (SAR)",
    "صفوف تحتاج مراجعة (هذه الصفحة)": "Rows needing review (this page)",
    "لا يوجد غياب في فترة الرواتب المحددة. 🎉": "No absences in the selected payroll period. 🎉",
    "فترات الغياب": "Absent blocks",
    "الغياب (س)": "Absent (h)",
    "مبلغ الخصم (ر.س)": "Deduction (SAR)",
    "ملاحظة": "Note",
    "مُستنتَج — أعد الاحتساب": "Synthesized — recalculate",
    "لا يوجد توزيع فترات": "No period breakup",
    "اعتماد الغياب وترحيل إجمالي كل موظف إلى الرواتب (D33) سيُضاف لاحقاً — هذه الشاشة للعرض والمراجعة حالياً.":
      "Approving absences and posting each employee's total to payroll (D33) comes later — this screen is for review for now.",

    // absence rates
    "مبالغ فترات الغياب": "Absence Period Rates",
    "المبلغ الشهري لكل فترة يُقسم على ٣٠ ليعطي خصم اليوم. البيانات المرجعية (الدرجات والمسميات والفترات) من SmartHCM.":
      "Each period's monthly amount divided by 30 gives the daily deduction. Reference data (grades, positions, periods) comes from SmartHCM.",
    "تعذّر جلب البيانات المرجعية.": "Could not load the reference data.",
    "مصفوفة المبالغ": "Rate matrix",
    "المسميات الوظيفية": "Positions",
    "المسمى الوظيفي": "Position",
    "كل الدرجات": "All grades",
    "المبالغ حالياً موحّدة لكل الدرجات (القيمة الافتراضية). تخصيص مبلغ لكل درجة سيُتاح بعد إنشاء جدول المبالغ.":
      "Rates are currently the same for every grade (the default). Per-grade rates become editable once the rate table exists.",
    "افتراضي": "Default",
    "موروث من الافتراضي": "Inherited from the default",
    "/ يوم": "/ day",
    "رقم الفترة": "Period ID",
    "الساعات المطلوبة": "Required hours",
    "المبلغ الشهري (ر.س)": "Monthly amount (SAR)",
    "خصم اليوم (ر.س)": "Daily deduction (SAR)",
    "غير مشمولة": "Not included",
    "الاسم المختصر": "Short name",
    "المستوى": "Level",
    "نوع الموظف": "Employee type",
    "الدرجات المسموحة": "Allowed grades",
    "إظهار غير النشطة": "Show inactive",
    "المسمى أو الرقم": "title or number",
    "عرض درجات هذا المسمى": "Show this position's grades",
    "غير نشط": "Inactive",
    "نشط": "Active",
    "الدرجات": "Grades",
    "الدرجة": "Grade",
    "مسمى": "positions",
    "درجة": "grades",

    // absence rates — editing
    "المبلغ الشهري لكل مسمى وظيفي وفترة يُقسم على ٣٠ ليعطي خصم اليوم. يعدّلها مسؤولو الموارد البشرية فقط، وتسري من شهر محدد دون تغيير الأشهر السابقة.":
      "Each position's monthly amount per period, divided by 30, gives the daily deduction. Only HR admins edit them; changes apply from a chosen month without altering earlier months.",
    "تعديل المبالغ": "Edit rates",
    "للعرض فقط — التعديل لمسؤولي الموارد البشرية": "Read-only — HR admins edit rates",
    "المبالغ السارية لفترة": "Rates in force for the period",
    "يسري من شهر": "Effective from month",
    "ملاحظة (اختياري)": "Remarks (optional)",
    "سبب التعديل": "reason for the change",
    "إضافة مسمى وظيفي": "Add position",
    "— اختر —": "— choose —",
    "إضافة": "Add",
    "المبالغ الجديدة تسري من الشهر المختار فما بعده، والأشهر السابقة تبقى بمبالغها. اترك الخانة فارغة لإلغاء المبلغ.":
      "New amounts apply from the chosen month onward; earlier months keep their amounts. Leave a cell empty to withdraw its amount.",
    "لا يمكن اختيار شهر قبل": "You cannot choose a month before",
    "لا يوجد مبلغ": "No rate",
    "عرض سجل التعديلات": "Show change history",
    "سجل التعديلات": "Change history",
    "لا يوجد سجل لهذه الخانة.": "No history for this cell.",
    "تعذّر جلب السجل.": "Could not load the history.",
    "تعذّر جلب المبالغ.": "Could not load the rates.",
    "يسري من": "Effective from",
    "بواسطة": "By",
    "أُلغي": "Withdrawn",
    "منذ": "since",
    "لا توجد تغييرات للحفظ.": "No changes to save.",
    "مبلغ غير صالح: ": "Invalid amount: ",
    "إلغاء": "Cancel",
    "حفظ": "Save",
    "تم حفظ": "Saved",
    "تغيير": "change(s)",
    "تعذّر الحفظ.": "Could not save.",
    "في تقرير الغياب": "In absence report",
    "مشمولة": "Included",

    // late / early
    "التأخر والخروج المبكر": "Late & Early Leaving",
    "دقائق التأخر والخروج المبكر لكل موظف/يوم/مجموعة فترات بعد دقائق السماح، وفق قواعد الاحتساب السارية للشهر. المصدر: SmartHCM عبر HR API.":
      "Late and early-leaving minutes per employee/day/period block beyond the grace minutes, under the calculation rules in force for the month. Source: SmartHCM via the HR API.",
    "نوع الفترة": "Range type",
    "شهر ميلادي (١ – آخر الشهر)": "Calendar month (1st – end)",
    "فترة الرواتب (١٦ – ١٥)": "Payroll period (16th – 15th)",
    "الشهر": "Month",
    "اختر الشهر.": "Choose the month.",
    "إظهار ما ألغاه الإذن الساعي": "Show blocks cancelled by hourly leave",
    "تعذّر جلب التأخر والخروج المبكر.": "Could not load late / early.",
    "إجمالي الدقائق (المبلغ)": "Total minutes (amount)",
    "دقائق التأخر": "Late minutes",
    "دقائق الخروج المبكر": "Early-leaving minutes",
    "ألغاها الإذن الساعي": "Cancelled by hourly leave",
    "لا يوجد تأخر أو خروج مبكر في النطاق المحدد. 🎉": "No lateness or early leaving in the selected range. 🎉",
    "المجموعات المتأخرة": "Late / early blocks",
    "بداية – نهاية": "Start – end",
    "الحضور": "Arrival",
    "الانصراف": "Departure",
    "الإذن الساعي": "Hourly leave",
    "الخروج المبكر": "Early leaving",
    "المبلغ (دقيقة)": "Amount (min)",
    "ألغاها الإذن": "Cancelled by leave",

    // calculation rules
    "قواعد الاحتساب": "Calculation Rules",
    "القواعد التي يُحسب بها الغياب (D33) والتأخر والخروج المبكر. يعدّلها مسؤولو الموارد البشرية فقط، وتسري من شهر محدد دون تغيير حساب الأشهر السابقة.":
      "The rules used to calculate absences (D33) and late / early leaving. Only HR admins change them; changes apply from a chosen month without recalculating earlier months.",
    "القواعد السارية لفترة": "Rules in force for the period",
    "معدّلة منذ": "Changed since",
    "معدّلة": "changed",
    "تعديل القواعد": "Edit rules",
    "القواعد الجديدة تسري من الشهر المختار فما بعده، والأشهر السابقة تُحسب بقواعدها.": "New rules apply from the chosen month onward; earlier months keep their rules.",
    "تعذّر جلب القواعد.": "Could not load the rules.",
    "القاعدة": "Rule",
    "القيمة": "Value",
    "الافتراضي": "Default",
    "الحالة": "Status",
    "سيُعاد للافتراضي": "Will reset to default",
    "إرجاع للقيمة الافتراضية": "Reset to default",
    "مفعّل": "On",
    "متوقف": "Off",
    "النطاق": "Scope",
    "مجموعات الفترات": "Period groups",
    "الغياب (D33)": "Absence (D33)",
    "يُطبّق على حساب الغياب والتأخر من هذا الشهر فما بعده.": "Applies to absence and late / early calculations from this month onward.",

    // absent report
    "تقرير الغياب اليومي": "Daily Absent Report",
    "تقرير الغياب": "Absent Report",
    "غياب الهيئة التعليمية والإدارية حسب مجموعات الفترات — صفّ لكل موظف/يوم/مجموعة. المصدر: SmartHCM.":
      "Teaching-staff / admin absences by period group — one row per employee/day/group. Source: SmartHCM.",
    "نطاق التقرير": "Report range",
    "من تاريخ": "From date",
    "إلى تاريخ": "To date",
    "الرقم الوظيفي (اختياري)": "Employee ID (optional)",
    "كل الموظفين": "all employees",
    "جلب التقرير": "Load report",
    "اختر التاريخين.": "Choose both dates.",
    "تعذّر جلب التقرير.": "Could not load the report.",
    "لا يوجد غياب في النطاق المحدد. 🎉": "No absences in the selected range. 🎉",
    "حالات الغياب": "Absences",
    "المجموعة": "Group",
    "الفترات": "Periods",
    "المطلوبة (س)": "Required (h)",
    "الحاضرة (س)": "Present (h)",
    "الإجازة (س)": "Leave (h)",
    "صباحية": "Morning",
    "مسائية": "Evening",
    "تسجيل تسوية حضور لهذا اليوم": "Record an attendance adjustment for this day",
    "تمت التسوية": "Adjusted",
    "تسوية حضور:": "Attendance adjustment:",
    "ملاحظة (اختياري):": "Notes (optional):",
    "تمت تسوية الحضور لـ": "Adjustment recorded for ",
    "فشلت التسوية: ": "Adjustment failed: ",
    "تسوية": "Adjust",

    // missing punches report
    "تقرير البصمات الناقصة": "Missing Punches Report",
    "البصمات الناقصة": "Missing Punches",
    "فترات الدوام التي سُجّلت فيها بصمة واحدة فقط — لا يمكن مطابقتها كدخول وخروج. تُستثنى أيام الغياب والإجازة. المصدر: SmartHCM.":
      "Shift periods with only one recorded punch — it cannot be paired as check-in and check-out. Absence and leave days are excluded. Source: SmartHCM.",
    "لا توجد بصمات ناقصة في النطاق المحدد. 🎉": "No missing punches in the selected range. 🎉",
    "بصمة الدخول مفقودة": "Missing check-in",
    "بصمة الخروج مفقودة": "Missing check-out",
    "دخول مفقود": "missing check-in",
    "خروج مفقود": "missing check-out",
    "الدخول": "Check-In",
    "الخروج": "Check-Out",
    "الناقص": "Missing",

    // employees — HR API import
    "أو الاستيراد من واجهة الموارد البشرية": "Or import from the HR API",
    "يجلب جميع الموظفين (الرقم الوظيفي، الاسم، المنصب) مباشرة من HR API — دون ملف. لا يُمسح المنصب المخزَّن إذا لم تُبلّغ الواجهة عنه.":
      "Fetches every employee (ID, name, position) straight from the HR API — no file needed. A stored position is never erased when the API doesn't report one.",
    "استيراد من HR API": "Import from HR API",
    "جارٍ الاستيراد...": "Importing...",

    // shell / nav / topbar
    "لوحة المعلومات": "Dashboard",
    "الحضور والدوام": "Attendance",
    "تسوية الحضور": "Attendance Reconciliation",
    "رفع البصمات": "Upload Punches",
    "جداول الدوام": "Rosters",
    "إدارة الموظفين": "Employee Management",
    "الصلاحيات والأدوار": "Roles & Permissions",
    "الموظفون": "Employees",
    "الموظفين": "Employees",
    "نظام الحضور والدوام": "Attendance & Time System",
    "تسجيل الخروج": "Sign out",
    "ابحث عن موظف أو رقم وظيفي...": "Search employee or ID...",
    "اللغة": "Language",
    "الإشعارات": "Notifications",
    "المساعدة": "Help",
    "الرئيسية": "Home",
    "مستخدم": "User",

    // login
    "تسجيل الدخول": "Sign in",
    "أدخل بياناتك للوصول إلى نظام الحضور والدوام": "Enter your credentials to access the attendance system",
    "اسم المستخدم (اختياري)": "Username (optional)",
    "اسم المستخدم": "Username",
    "كلمة المرور": "Password",
    "تذكّرني": "Remember me",
    "نسيت كلمة المرور؟": "Forgot password?",
    "جارٍ الدخول...": "Signing in...",
    "موثّق عبر واجهة HrAuth": "Authenticated via HrAuth",
    "بيانات الدخول غير صحيحة.": "Invalid credentials.",
    "تعذّر الاتصال بالخادم.": "Could not connect to the server.",
    "رفع بصمات أجهزة الحضور، ومطابقتها مع جداول الدوام، ورصد التأخير والانصراف المبكر والبصمات الناقصة — في منصّة واحدة.":
      "Upload attendance-device punches, match them against rosters, and track lateness, early departures, and missing punches — in one platform.",

    // dashboard
    "حياكم الله، ": "Welcome, ",
    "بك": "there",
    "الأربعاء · ٢٤ ذو القعدة ١٤٤٧ هـ": "Wednesday · 24 Dhul-Qa'dah 1447 AH",
    "٢٤ ذو القعدة ١٤٤٧": "24 Dhul-Qa'dah 1447",
    "إجمالي الموظفين": "Total employees",
    "حاضر اليوم": "Present today",
    "الحاضرون اليوم": "Present today",
    "تنبيهات مفتوحة": "Open alerts",
    "إجراءات سريعة": "Quick actions",
    "الجداول": "Rosters",
    "حضور اليوم": "Today's attendance",
    "حالات تأخير (الأسبوع)": "Late cases (week)",
    "بصمات ناقصة للمراجعة": "Missing punches to review",
    "في ٧ أقسام": "in 7 departments",
    "انضباط": "punctuality",
    "تتطلب إجراء": "Action required",
    "الحضور حسب القسم — اليوم": "Attendance by department — today",
    "أحدث النشاطات": "Latest activity",
    "عرض التسوية": "View reconciliation",
    "تم رفع ملف بصمات جديد": "A new punch file was uploaded",
    "٤ بصمات ناقصة تتطلب المراجعة": "4 missing punches need review",
    "ضمن آخر عملية تسوية": "In the last reconciliation",
    "٦ حالات تأخير هذا الأسبوع": "6 late cases this week",
    "بإجمالي ١٢٢ دقيقة": "totaling 122 minutes",
    "تمت إضافة موظف جديد": "A new employee was added",
    "وليد سعيد القرني": "Walid Saeed Al-Qarni",
    "٥٤ سطر": "54 lines",
    "اليوم": "Today",
    "أمس": "Yesterday",
    // department names (sample data)
    "تقنية المعلومات": "IT",
    "التشغيل والصيانة": "Operations & Maintenance",
    "المالية": "Finance",
    "الشؤون الأكاديمية": "Academic Affairs",
    "خدمة المستفيدين": "Beneficiary Services",
    "الموارد البشرية": "Human Resources",
    "الأمن والسلامة": "Security & Safety",

    // reconciliation
    "مطابقة بصمات الحضور مع جداول الدوام — البصمات الناقصة والتأخير والانصراف المبكر. اضغط أي بطاقة أو صف للتفاصيل.":
      "Match attendance punches against rosters — missing punches, lateness, and early departures. Click any card or row for details.",
    "مطابقة بصمات الحضور مع جداول الدوام — البصمات الناقصة والتأخير والانصراف المبكر.":
      "Match attendance punches against rosters — missing punches, lateness, and early departures.",
    "لا توجد وردية مجدولة في هذا التاريخ — لم تُطابق البصمات مع أي جدول دوام.":
      "No shift scheduled on this date — punches did not match any roster.",
    "بصمة ناقصة — لا بصمة ضمن ±١٥ دقيقة": "Missing punch — none within ±15 min",
    "جميع الفترات المجدولة تم حضورها في الوقت المحدد.": "All scheduled periods were attended on time.",
    "لا توجد سجلات مطابقة لمعايير التصفية الحالية.": "No records match the current filter.",
    "الفترات المجدولة — ✓ منضبط / ✗ ناقصة · تأخير": "Scheduled periods — ✓ on time / ✗ missing · late",
    "لا توجد وردية مسجّلة": "No registered shift",
    "لا توجد نتيجة تسوية بعد": "No reconciliation result yet",
    "ارفع ملف بصمات أجهزة الحضور لعرض تسوية الدوام.": "Upload a punch file to view reconciliation.",
    "انصراف مبكر": "Early departure",
    "بصمة ناقصة": "Missing punch",
    "تصدير Excel": "Export Excel",
    "طباعة التقرير": "Print report",
    "نتائج التسوية": "Reconciliation results",
    "تغييرات غير مطبّقة": "Unapplied changes",
    "ابحث بالرقم الوظيفي أو الاسم": "Search by ID or name",
    "من تاريخ": "From date",
    "إلى تاريخ": "To date",
    "تطبيق التصفية": "Apply filter",
    "إعادة تعيين": "Reset",
    "التصفية": "Filter",
    "في الوقت المحدد": "On time",
    "ملف الموظف": "Employee profile",
    "الوردية: ": "Shift: ",
    "أول بصمة": "First punch",
    "آخر بصمة": "Last punch",
    "عدد البصمات": "Punch count",
    "الفترات المجدولة": "Scheduled periods",
    "الرقم الوظيفي": "Employee ID",
    "المجدول": "Scheduled",
    "سجل دوام": "Attendance records",
    "بصمات ناقصة": "Missing punches",
    "دقائق التأخير": "Late minutes",
    "دقائق الانصراف المبكر": "Early-departure minutes",
    "بدون وردية": "No shift",
    "انصراف مبكر ": "Early departure ",
    "تأخير ": "Late ",
    "انصراف ": "Departure ",
    "متأخر": "Late",
    "منضبط": "On time",
    "ناقصة": "missing",

    // roster / upload / employees / roles screens
    "عرض وردية الموظف وفتراته المجدولة، المجلوبة من خدمة جداول الدوام (SmartHCM).":
      "View the employee's shift and scheduled periods, fetched from the roster service (SmartHCM).",
    "بحث عن وردية": "Find shift",
    "جلب الوردية": "Fetch shift",
    "أدخل الرقم الوظيفي.": "Enter the employee ID.",
    "تعذّر جلب الوردية.": "Could not fetch the shift.",
    "لا توجد وردية مسجّلة للموظف ": "No registered shift for employee ",
    " بتاريخ ": " on ",
    "الساعات المطلوبة": "Required hours",
    "إجمالي ساعات الوردية: ": "Total shift hours: ",
    " ساعات": " hours",
    "البداية": "Start",
    "النهاية": "End",
    "وردية": "Shift",

    "رفع ملف بصمات أجهزة الحضور. تُوحَّد الأرقام الوظيفية إلى ٨ خانات عند الرفع.":
      "Upload an attendance-device punch file. Employee IDs are normalized to 8 digits on upload.",
    "اختيار الملف": "Choose file",
    "صيغة السطر المتوقعة: ": "Expected line format: ",
    " — مثال: ": " — example: ",
    "جارٍ المعالجة...": "Processing...",
    "اسحب ملف البصمات هنا أو اضغط للاختيار": "Drag the punch file here or click to choose",
    "يدعم الصيغ ": "Supports ",
    "اختيار ملف": "Choose file",
    "فشل الرفع (": "Upload failed (",
    "نتيجة المعالجة — ": "Processing result — ",
    "اكتملت المعالجة": "Processing complete",
    "سطر مقروء": "Lines read",
    "بصمة مستوردة": "Punches imported",
    "سطر متجاهَل": "Lines skipped",
    "عرض تسوية الحضور": "View reconciliation",
    "الرقم الأصلي": "Original ID",
    "الرقم الموحَّد (٨)": "Normalized ID (8)",
    "التوقيت": "Timestamp",
    "رمز العمل": "Work code",
    "التحقق": "Verify",
    "لا توجد بصمات في الملف.": "No punches in the file.",
    "دخول": "In",
    "خروج": "Out",

    "استعراض الموظفين أو إضافتهم أو استيرادهم، ليتمكّن النظام من ربط بصمات الحضور بأسماء الموظفين.":
      "Browse, add, or import employees so the system can link punches to names.",
    " في الدليل": " in directory",
    "استيراد CSV": "Import CSV",
    "إضافة موظف جديد": "Add new employee",
    "إضافة موظف": "Add employee",
    "الاسم الكامل": "Full name",
    "المنصب (اختياري)": "Position (optional)",
    "رقم المسؤول المباشر (اختياري)": "Direct manager ID (optional)",
    "أرقام — تُكمَّل إلى ٨ خانات": "Digits — padded to 8",
    "الاسم بالعربية": "Name in Arabic",
    "المسمّى الوظيفي": "Job title",
    "الرقم الوظيفي للمسؤول": "Manager's employee ID",
    "الرقم الوظيفي والاسم مطلوبان.": "Employee ID and name are required.",
    "الرقم الوظيفي أو الاسم أو اسم المستخدم": "ID, name, or username",
    "جارٍ التحميل…": "Loading…",
    "لا توجد نتائج.": "No results.",
    "تعذّر التحميل: ": "Load failed: ",
    "المسؤول المباشر": "Direct manager",
    "اسم المستخدم": "Username",
    "المنصب": "Position",
    "الأدوار": "Roles",
    "لا يوجد موظفون.": "No employees.",
    "مزامنة المنصب والمسؤول من SmartHCM": "Sync position & manager from SmartHCM",
    "السابق": "Previous",
    "التالي": "Next",
    "صفحة ": "Page ",
    "رفع ملف الدليل (CSV)": "Upload directory file (CSV)",
    "الصيغ المقبولة: ": "Accepted formats: ",
    " ترميز UTF-8 فقط. يُتخطّى صف العناوين تلقائياً. تُكمَّل الأرقام إلى ٨ خانات.":
      " UTF-8 encoding only. The header row is skipped automatically. IDs are padded to 8 digits.",
    "اسحب ملف CSV هنا": "Drag the CSV file here",
    "الموظفون الحاليون يحتفظون باسم المستخدم والأدوار — يُحدَّث الاسم فقط":
      "Existing employees keep their username and roles — only the name is updated",
    "فشل الاستيراد (": "Import failed (",
    "صف مكتوب": "Rows written",
    "صف متجاهَل": "Rows skipped",
    "الإجمالي في القاعدة": "Total in database",
    "تمت إضافة ": "Added ",
    "تمت مزامنة ": "Synced ",
    " من SmartHCM": " from SmartHCM",
    "فشل المزامنة: ": "Sync failed: ",

    "عرّف الأدوار في الكتالوج، ثم عيّنها للموظفين للتحكّم في صلاحيات الوصول.":
      "Define roles in the catalog, then assign them to employees to control access.",
    "كتالوج الأدوار": "Roles catalog",
    "تعيين الموظفين": "Assign employees",
    "اسم الدور مطلوب.": "Role name is required.",
    "اسم الدور": "Role name",
    "الوصف (اختياري)": "Description (optional)",
    "إضافة دور": "Add role",
    "مثال: مشرف": "e.g., Supervisor",
    "وصف موجز لصلاحيات الدور": "Brief description of the role",
    "عدد المعيَّنين": "Assigned count",
    "لا توجد أدوار بعد — أضف دوراً أعلاه.": "No roles yet — add one above.",
    "الأدوار (اضغط للتبديل ثم احفظ)": "Roles (tap to toggle then save)",
    "عرّف دوراً واحداً على الأقل في كتالوج الأدوار أولاً.": "Define at least one role in the catalog first.",
    "أُضيف الدور": "Added role",
    "حُفظ ": "Saved ",
    "فشل الحفظ: ": "Save failed: ",
    "حُذف ": "Deleted ",
    "فشل الحذف: ": "Delete failed: ",
    "حذف الدور ": "Delete role ",
    "؟ سيُزال أيضاً من كل موظف معيَّن له.": "? It will also be removed from every assigned employee.",
    "تعذّر تحميل الموظفين: ": "Could not load employees: ",
    " دور.": " role(s).",
    "حفظ": "Save",
    "الوصف": "Description",

    // shared / generic (longer entries first via sort)
    "الرقم الوظيفي": "Employee ID",
    "الاسم": "Name",
    "الموظف": "Employee",
    "التاريخ": "Date",
    "اليوم": "Day",
    "الوردية": "Shift",
    "البصمات": "Punches",
    "الفترة": "Period",
    "الحالة": "Status",
    "الدليل": "Directory",
    "بحث": "Search",
    "موظفون": "employees",
    "موظف": "employee",
    "ساعات": "hours",
    "بصمات": "punches",
    "بصمة": "punch",
    "دقائق": "minutes",
    "دقيقة": "minute",
    "من ": "of ",
    "عرض ": "Showing ",
    " سجل": " records",
    "ملف ": "File ",
  };

  // Sort keys longest-first so multi-word phrases win over their fragments.
  var KEYS = Object.keys(DICT).sort(function (a, b) { return b.length - a.length; });
  var ARABIC = /[؀-ۿ]/;

  function apply(text) {
    if (!text || !ARABIC.test(text)) return text;
    var out = text;
    for (var i = 0; i < KEYS.length; i++) {
      if (out.indexOf(KEYS[i]) !== -1) out = out.split(KEYS[i]).join(DICT[KEYS[i]]);
      if (!ARABIC.test(out)) break;
    }
    return out;
  }

  function translate(root) {
    if (!root) return;
    var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null, false);
    var nodes = [], n;
    while ((n = walker.nextNode())) nodes.push(n);
    for (var i = 0; i < nodes.length; i++) {
      var v = nodes[i].nodeValue;
      if (v && ARABIC.test(v)) { var t = apply(v); if (t !== v) nodes[i].nodeValue = t; }
    }
    var attrEls = root.querySelectorAll ? root.querySelectorAll("[placeholder],[title]") : [];
    for (var j = 0; j < attrEls.length; j++) {
      var el = attrEls[j];
      if (el.placeholder && ARABIC.test(el.placeholder)) el.placeholder = apply(el.placeholder);
      if (el.title && ARABIC.test(el.title)) el.title = apply(el.title);
    }
  }

  function start() {
    var root = document.getElementById("root") || document.body;
    if (document.title) document.title = apply(document.title);
    translate(root);

    var scheduled = false, observer;
    observer = new MutationObserver(function () {
      if (scheduled) return;
      scheduled = true;
      window.requestAnimationFrame(function () {
        scheduled = false;
        observer.disconnect();
        translate(root);
        observer.observe(root, { childList: true, subtree: true, characterData: true });
      });
    });
    observer.observe(root, { childList: true, subtree: true, characterData: true });
  }

  if (document.readyState === "loading")
    document.addEventListener("DOMContentLoaded", start);
  else
    start();
})();
