# Plan — Absence approval → D33 monthly deduction

**Status:** Slice 1 (paged list) + lookups (grades, positions, periods) implemented on `feature/absence-approval`. Approve / post-month / rate table still blocked on Q1, Q4, Q6.
**Date:** 2026-09-29
**Owner:** imran-MED
**Host:** HrApi (`src/HrApi.*`). This feature writes to SmartHCM (Oracle), and only
HrApi has that connection. `HrAuth.CleanArchitecture` wraps the finished endpoints
afterwards (Slice 5).

---

## 1. Goal

HR admins page through employees' unexcused absences. For each absence they approve it
as a deduction or reject it (excused / data error). **An approved absence is posted to SmartHCM payroll and becomes
part of the employee's monthly D33 deduction at the next payroll run**, with
no manual re-entry in the SmartHCM payroll screens.

## 2. Starting point (already on `test` / live)

| Piece | Where | Reuse |
|---|---|---|
| Absence query (older version of §3.1) | `DailyAbsentReportService` | SQL replaced by §3.1, then paged |
| `GET /api/hr/reports/daily-absent` | `ReportsController` | Stays as-is (unpaged report) |
| SmartHCM per-screen rights (`[RequireActivityRight(module, activity, right)]`) | `HrActivityRightsService`, commit `0659266` | Gates approve/reject |
| Policies (`ReportsReadPolicy`, service tokens) | `AuthorizationExtensions` | Read endpoint |
| `PagedResult<T>` | `HrApi.Application/Models/PagedResult.cs` | Paged response |

Note: HrApi's SmartHCM services use raw ADO.NET (`Oracle.ManagedDataAccess`), not
EF Core / Unit of Work. This plan keeps that established pattern. The atomic write
uses one `OracleTransaction`, following CLAUDE.md's Begin/Commit/Rollback shape.

## 3. How teaching-staff D33 works today (from `D:\smartHCM\09-2026`, Sep 2026 cycle)

```
Daily_Absent_Report_PlainSelect_16Aug_15Sep2026.sql     (absences, grouped by period block, with amount)
        │  export → Excel (غياب كادر تعليمي.xlsx) → HR corrects/approves
        ▼
"D33 MEN2 - original lines.csv"   (Employee ID, المبلغ) — 97 lines, one per absent day/block
        │  Pay_S017 upload → PAY_MONTHLY_ALLOW_DED_SESS → SAVE_UPDATE_ALLOWNCE_DEDUCTION_FROM_SESSION → PAY_MONTHLY_ALLOW_DEDUCT (§3.3)
        ▼
PAY_EMP_PAYROLL (EMPLOYEE_ID, ADCODE='D33', PAY_MONTH=15-09-2026, PAY_AMOUNT = sum of lines)
        │  checked by Check_D33_Payroll_Teaching_Absent_15Sep2026.sql (27 employees, 8,025)
        ▼
PAY_SALARY_SHEET_VW → bank file
```

So **the feature automates the manual Excel → CSV → upload step.** The API lists the
absences this query finds, HR approves them in the app instead of in Excel, and each
approved block becomes a D33 line for the payroll month.

### 3.1 Absence rules (the source query, to be ported as-is and parameterised)

| Rule | Detail |
|---|---|
| Population | positions 118/119/120, `type_id = 361`, **gender filter** (the Sep file is `'M'`, the ladies' file is separate) → becomes a `gender` parameter |
| Window | payroll period **16th → 15th** (upper bound exclusive); `pay_month` = the 15th |
| Periods | `period_id IN (1,2,3,4,5,6,56,89)` from `TOM_PERIOD_ATT_WITH_LEAVE_VW` |
| Grouping ("period combine") | 1–3 → `GRP_MORNING`, 4–6 → `GRP_EVENING`, others → `P_<id>`; `LISTAGG` of short names, period count, hours per group |
| Absent | `attendance_status = 'A'`, `required_hours > 0`, and **no punch anywhere in the block** (morning < 15:00, evening ≥ 15:00, other periods inside the breakup times). A punch in the block = missing punch, not absent |
| Hourly leave | `absent_hours = required_hours − leave_hours` (floored at 0); fully covered periods drop out |
| **Amount** | `period_rate × absent_hours / required_hours`, where `period_rate` = monthly period allowance ÷ 30. The query hard-codes P1 1300, P2 1500, P3 1300, P4 1500, P5 1100, P6 1300, P56 1300, P89 2800 for everyone. **These move to the per-grade rate table (§5.5)**, seeded with these values |
| Flag: no breakup row | `TOM_PERIOD_WISE_BREAKUP_DET` has no row for that month → times unknown |
| Branch 2: synthesized days | employee has **no view rows at all** that day, no punch, no approved leave, and other employees do have rows (so it isn't a holiday). The pattern is copied from the employee's nearest day, and the row is flagged **RECALCULATE** |

Differences from the current `DailyAbsentReportService` (HrApi `test`): it lacks periods
56/89, the gender filter, the punch-in-block rule, hourly-leave proration, the amount,
both flags and branch 2. **Slice 1 replaces its SQL with this query.** The existing
`/reports/daily-absent` endpoint should then return the same rows.

Hard-coded rates are replaced by the **grade × period rate table** in §5.5. The query looks
up each period's monthly amount by the employee's `HRS_EMPLOYEE_PERSONNEL.GRADE`.

### 3.2 Payroll-side facts (from `D:\dll`, July–Aug 2026 investigation)

- `PAY_MONTH` / `ATTEND_MONTH` = the 15th. Closed = the `PAY_PROCESS` row for that
  month / location / type is **authorized**. Corrections to closed months go through
  `PAY_BACKDATED_ATTEND_ADJ`, or were made directly on `PAY_EMP_PAYROLL` (Aug fix).
- A **second** D33 source exists for slot/prayer rosters: the payroll run posts
  `ABS_CNT_*` → "Total Br. Abs." from `PAY_EMP_ATTEND` and prices it as D33. We must
  confirm that teaching staff (118/119/120) do **not** also get D33 this way, or they would
  be charged twice (Q1).

### 3.3 The upload write path (decompiled from `D:\dll\hcm_uat\bin\SmartHCM.dll`)

Screen **PAY/Pay_S017 "Monthly Data Upload"** (Load Type = Deduction, Code = D33,
month `MM-YYYY`, comma-separated file) posts to
`PayController.SaveMonthlyDataUploadAD_backup`. This is the action the screen actually
calls: the method named `SaveMonthlyDataUploadAD` is a stub, and `_backup2` is dead code.

1. `SESSION_ID` ← `PAY_MONTHLY_ALLOW_DED_SESS_ID.NEXTVAL` (one per upload).
2. For each file line, `ID` ← `PAY_MONTHLY_ALLOW_DED_SESS_SEQ.NEXTVAL`, then an array-bound insert:
   ```sql
   INSERT INTO PAY_MONTHLY_ALLOW_DED_SESS
     (ID, SESSION_ID, ADCODE, EMPLOYEE_ID, PAY_AMOUNT, PAY_MONTH,
      PREPARED_BY, PREPARED_ON, AUTHORIZED_ON, AUTHORIZED_BY)
   VALUES (:1, :2, :3, :4, :5, :6, :7, :8, :9, :10)
   ```
   (`PAY_MONTH` is the screen's `MM-YYYY` string. Prepared and authorized are both the uploader, now.)
3. **Duplicate guard.** Session lines are deleted and reported "Record already exist in
   database" when `PAY_MONTHLY_ALLOW_DEDUCT` already has an **authorized** row for the same
   `ADCODE` + `EMPLOYEE_ID` + `PAY_MONTH = get_pay_last_date(employee_id, pay_month)`.
4. `SAVE_UPDATE_ALLOWNCE_DEDUCTION_FROM_SESSION(P_SESSION_ID, P_LOAD_TYPE => 'D')`
   moves the session lines into **`PAY_MONTHLY_ALLOW_DEDUCT`** (keyed by adcode +
   employee + pay_month, so the 97 lines were summed to 27 employee totals).
5. The payroll run reads `PAY_MONTHLY_ALLOW_DEDUCT` → `PAY_EMP_PAYROLL` D33 → bank file.

**Consequence:** because of step 3, once an employee's D33 for a month is in
`PAY_MONTHLY_ALLOW_DEDUCT` (authorized), **a second upload for that employee/month is
rejected**. Approvals therefore cannot each be pushed as they happen. They accumulate,
and one **"post month"** action sends every approved, unposted line for the month in a
single session, the same way the CSV did. (If `SAVE_UPDATE_ALLOWNCE_DEDUCTION_FROM_SESSION`
turns out to add to an existing row, or HR needs a top-up after posting, see Q1.)

## 4. Open questions

| # | Question | Why it matters |
|---|---|---|
| Q1 | **Answered (§3.3)**: session table + `SAVE_UPDATE_ALLOWNCE_DEDUCTION_FROM_SESSION`. Still to confirm by reading the procedure source (`SELECT text FROM all_source WHERE name = 'SAVE_UPDATE_ALLOWNCE_DEDUCTION_FROM_SESSION' ORDER BY line`) and `get_pay_last_date`: does it insert only, or add to an existing row? What happens to late approvals after the month is posted (next month, or a manual top-up)? Do teaching staff also get automatic "Total Br. Abs." D33? | Decides whether posting is once per month or can repeat. |
| Q2 | **Answered by §5.5:** rates are configured per grade × period in our own table, editable from a page. Still to confirm: is `HRS_EMPLOYEE_PERSONNEL.GRADE` the right grade (vs position/category)? Do the ladies' rates differ only by grade, or do we also need gender in the key? | Decides the rate table key. |
| Q3 | Does HR approve **per block** (one CSV line each, as today) or per employee total? Can they **edit the amount** (the "بعد التعديل / قبل التصحيح" sheets suggest they correct rows)? | Decides whether the API needs an "adjust amount" field. |
| Q4 | **Which SmartHCM screen/right** makes someone an "HR admin" for this? (module + activity id, like Adjust-Attendance = TOM/S010) | Needed for `[RequireActivityRight]`. |
| Q5 | Can an approval be **reversed** before payroll closes? | Decides whether Slice 4 is needed. |
| Q6 | Where may we store **approval state** (pending/approved/rejected, who, when)? A new table in the SmartHCM schema (needs DBA), or a HrApi-owned DB? | Needed for the pending filter, idempotency and the audit trail. |
| Q9 | **Which frontend hosts the HR pages** (absence approval + rate configuration)? HrApi is API-only. Is it an existing HR admin web app, or a new one? Who builds the UI? | The page is part of this scope only if we own that frontend. |
| Q7 | How should **branch-2 (RECALCULATE)** rows be handled: hidden, shown but not approvable until recalculated, or approvable? | They have no real leave data, so their amount may be wrong. |
| Q8 | **Rounding of the employee total:** round once (sum exact block amounts → whole SAR, half-up), which is the plan's default? The Sep-2026 CSV rounded **each line by hand, inconsistently** (86.67 entered as 86 for 00000925 but as 87 for 00003026). So the API may differ from the old totals by ±1 SAR per employee. | Fixes the reconciliation rule for Slice 3 and stops per-line rounding drift. |

## 5. API design

All routes under `api/hr/absences`. Every endpoint takes a `CancellationToken` and
returns the standard `ApiResponse<T>` envelope.

### 5.1 `GET /api/hr/absences` — paged list

Query (`AbsenceQuery`): `payMonth` (e.g. `2026-09`, meaning 16-Aug → 15-Sep; or explicit
`from`/`to` within one period), `gender?` (`M` | `F`), `employeeId?`, `department?`,
`status?` (`pending` default | `approved` | `rejected` | `all`), `includeSynthesized?`
(Q7), `pageNumber` (≥1, default 1), `pageSize` (1–100, default 25).

Response also carries the page's `total_amount` and the query-wide `grand_total_amount`,
so HR sees what the month will deduct.

Response: `PagedResult<AbsenceRecordDto>` with `items`, `pageNumber`, `pageSize`,
`totalCount` and `totalPages`.

```jsonc
{
  "absence_id": "EMP00123-2026-09-14-GRP_MORNING", // stable composite key
  "employee_id": "EMP00123",
  "employee_name": "…",
  "position": "…",
  "department": "…",
  "attend_date": "2026-09-14",
  "group_key": "GRP_MORNING",
  "periods": "P1+P2+P3",            // "period combine" (LISTAGG)
  "period_count": 3,
  "required_hours": 3.0,
  "present_hours": 0.0,
  "leave_hours": 0.0,
  "absent_hours": 3.0,
  "deduction_amount": 136.67,      // rate × absent/required, per §3.1
  "flag": null,                    // null | "NO_BREAKUP_ROW" | "SYNTHESIZED_RECALCULATE"
  "status": "pending",            // pending | approved | rejected
  "decided_by": null, "decided_at": null,
  "deduction": null               // { adcode: "D33", pay_month: "2026-09-15", amount } once approved
}
```

- Paging happens in Oracle (`OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY` +
  `COUNT(*) OVER ()`), never in memory.
- The approval table is left-joined in so `status` is filtered in SQL.
- Policy: `ReportsReadPolicy`.

### 5.2 `POST /api/hr/absences/approve`

```jsonc
{ "absence_ids": ["EMP00123-2026-09-14-GRP_MORNING", "…"], "remarks": "optional, ≤500" }
```

- Gated by `[RequireActivityRight(<Q4 module>, <Q4 activity>, Authorize)]`.
- Per id, in **one Oracle transaction**:
  1. Re-run the §3.1 query for that one employee / day / block. The absence must still
     exist, and its amount is recomputed server-side (the client never sends the amount).
     Attendance may have been adjusted since the list was loaded.
  2. Check that it isn't already decided (unique key on employee + date + group, so it
     is idempotent).
  3. Check that the payroll month is open (Q3).
  4. Write the approval row (who, when, remarks).
  5. Store the server-computed amount and the pay month on the approval row, with status
     `approved` and **`posted = N`**. Nothing is written to SmartHCM payroll yet (see §3.3 for why).
  6. Commit. On any failure, roll back that id and report it.
- Response: `200` with a per-id result `{ absence_id, outcome: approved | already_decided | not_absent | payroll_closed | failed, deduction_reference? }`.
  One bad id doesn't block the rest. Each id has its own transaction, so no half-written pairs are left behind.

### 5.3 `POST /api/hr/absences/post-month` — send approved lines to payroll

```jsonc
{ "pay_month": "2026-09", "gender": "M" }
```

- Separate, stronger right (Q4), e.g. `Authorize` on the payroll upload screen PAY/S017.
- Guards: the payroll period for that month is not authorized/closed (`PAY_PROCESS`);
  there are approved, unposted lines; none of those employees already has an authorized
  D33 row in `PAY_MONTHLY_ALLOW_DEDUCT` for the month (otherwise report them, as the screen does).
- **One line per employee holding the employee's total.** The approved, unposted blocks
  for the month are summed per employee in SQL (`GROUP BY employee_id`), using the stored
  unrounded block amounts. The total is then rounded **once** to whole SAR, half-up (Q8).
  This matches `PAY_MONTHLY_ALLOW_DEDUCT`, which holds one D33 row per employee per month,
  and the Sep-2026 baseline (27 employees → 27 totals).
- Writes exactly what Pay_S017 writes (§3.3 steps 1–4): new `SESSION_ID`, then one
  `PAY_MONTHLY_ALLOW_DED_SESS` row **per employee** (`ADCODE='D33'`, `PAY_AMOUNT` =
  employee total, `PAY_MONTH='MM-YYYY'`, prepared/authorized = the posting HR user), then
  `SAVE_UPDATE_ALLOWNCE_DEDUCTION_FROM_SESSION(session, 'D')`, all in **one transaction**.
  Then it marks every contributing approval row `posted = Y` with the `SESSION_ID`, so
  each employee total can be traced back to its blocks.
- Response, one entry per employee:
  ```jsonc
  {
    "session_id": 123456,
    "pay_month": "2026-09",
    "employees_posted": 27,
    "grand_total": 8025,
    "employees": [
      { "employee_id": "00000163", "employee_name": "…", "absent_blocks": 20,
        "total_amount": 1800, "outcome": "posted" }
      // outcome: posted | rejected_already_posted (authorized D33 row exists) | failed
    ]
  }
  ```
- `GET /api/hr/absences/post-month/preview?pay_month=&gender=` returns the same
  per-employee totals without writing, so HR can compare with the old Excel.

### 5.4 `POST /api/hr/absences/reject`

Same body with required `remarks`. Writes an approval row with status `rejected` and
no deduction. Same right gate.

### 5.5 Period rate configuration (grade × period), plus its admin page

HR maintains **the monthly amount for each period, per grade**, instead of the literals in
the SQL. Daily rate = monthly ÷ 30, exactly as now.

**Table** `HRAPI_ABSENCE_PERIOD_RATE` (lives next to the approval table, Q6):

| Column | Notes |
|---|---|
| `RATE_ID` | PK (sequence) |
| `GRADE` | FK → `HRS_GRADE.GRADE`. **NULL = default row** used when a grade has no specific rate |
| `PERIOD_ID` | FK → `TOM_PERIOD_WISE_BREAKUP.PERIOD_ID` (1–6, 56, 89 today) |
| `MONTHLY_AMOUNT` | NUMBER(10,2), > 0 |
| `EFFECTIVE_FROM` | pay month (the 15th) the rate starts applying |
| `EFFECTIVE_TO` | NULL = current. Set automatically when a newer version is saved |
| `PREPARED_BY/ON`, `AUTHORIZED_BY/ON`, `REMARKS` | same audit columns SmartHCM uses |

Unique `(GRADE, PERIOD_ID, EFFECTIVE_FROM)`. Seeded with today's values as `GRADE = NULL`
default rows effective from 15-09-2026, so **nothing changes on day one**.

Rules:
- **Effective-dated, never overwritten.** A change creates a new version from a pay month
  onward, so a past month always recalculates with the rates it was paid with.
- **Can't change the past:** `EFFECTIVE_FROM` must be a month that has not been posted
  (`post-month`) and whose payroll is not authorized.
- **Lookup order** per absent period: exact grade + period → default (NULL grade) + period →
  **no rate**. A missing rate never means 0. The row gets flag `NO_RATE` and cannot be approved.
- **Snapshot at approval:** the approval row stores the monthly amount and rate version it
  used. Editing rates later doesn't change approved or posted amounts. Unapproved rows
  recalculate on the next read.

**Endpoints** (`api/hr/absence-rates`):

| Method | Route | Purpose | Right |
|---|---|---|---|
| GET | `/grades?positionId=` | **all** SmartHCM grades (or only those a position allows), read live; see **Grades source** below | read |
| GET | `/positions?includeInactive=` | **all** SmartHCM positions (`HRS_POSITION`) with head-count and allowed grades | read |
| GET | `/periods` | every period in `TOM_PERIOD_WISE_BREAKUP` (id, short name, description, required hours, order) with its configured monthly/daily rate (null if not in the report) | read |
| GET | `?payMonth=2026-10` | the **matrix** in force for that month: one row per grade (+ Default), one cell per period `{ monthly_amount, daily_rate, source: grade \| default \| none, rate_id }` | read |
| PUT | `/` | save a matrix: `{ effective_from: "2026-10", remarks, cells: [{ grade, period_id, monthly_amount }] }`. Upserts as a new version, all in one transaction | edit (Q4) |
| GET | `/history?grade=&periodId=` | the versions for a grade/period: who, when, old → new | read |
| GET | `/impact?payMonth=&gender=` | before saving: how unposted pending/approved totals for that month would change | read |

**Grades and positions source.** Read from SmartHCM through the API, never typed in. The
tables (from the decompiled `SmartHCM.Core` NHibernate maps) are:

| Table | Key / columns used |
|---|---|
| `HRS_GRADE` | `GRADE` (PK, VARCHAR2(3)), `SHORT_NAME`, `GRADE_DESCRIPTION`, `LEVEL_NO`, `TYPE_ID` |
| `HRS_POSITION` | `POSITION_ID`, `TITLE`, `ACTIVE` |
| `HRS_POSITION_GRADE` | `GRADE` + `POSITION_ID`: which grades a position allows |
| `HRS_EMPLOYEE_PERSONNEL` | `GRADE`, `POSITION_ID`: used for head-counts |

- `GET /grades` returns **every grade** (not only 118/119/120), ordered by `LEVEL_NO`, each
  with its employee head-count. `?positionId=` narrows the list to that position's grades
  and counts only within it.
- `GET /positions` returns **every position**, each with its head-count and allowed grade
  codes, so the page can pick a position and then its grades.
- Q2 (rate key) is now **grade and/or position × period**: confirm whether rates depend on
  the grade alone, or on position + grade.

**Page** (in the HR admin frontend, Q9):
- A grid with **grades as rows** (plus a "Default" row at the top) and **periods as
  columns** (الفجر P1, الضحى P2, الظهر P3, بعد الظهر P56, العصر P4, المغرب P5, العشاء P6, P89).
  Each cell shows the monthly amount, with the daily rate (÷30) underneath.
- Cells that inherit from Default are shown greyed out. Typing a value makes it grade-specific,
  and clearing it goes back to Default.
- Pick the "effective from" pay month at the top. Past and posted months are read-only.
- Save shows a confirmation with changed cells (old → new) and the `/impact` totals, then PUTs.
- A history drawer per cell, and filter or search by grade.
- Arabic/English labels, RTL layout.

### 5.6 (Slice 4, if Q5 = yes) `POST /api/hr/absences/{absenceId}/revoke`

Allowed only while the approval is **not yet posted**. It marks the approval `revoked`.
After posting, a correction has to be made in SmartHCM (or via Q1's top-up rule).

## 6. Layering (HrApi)

| Layer | New items |
|---|---|
| Core | `AbsenceDecisionStatus` enum, screen constant in `HrScreens` (Q4), `AbsenceId` value parser/formatter |
| Application | `IAbsenceApprovalService`; DTOs `AbsenceQuery`, `AbsenceRecordDto`, `AbsenceDecisionRequest` (init-only, validated), `AbsenceDecisionResultDto`, `DeductionInfoDto`; `IAbsenceRateService` + `RateMatrixDto`, `SaveRateMatrixRequest`, `RateHistoryDto`; FluentValidation validators |
| Services / Infrastructure | `AbsenceApprovalService` (Oracle, one transaction per id), `IDeductionWriter` / `SmartHcmDeductionWriter` (isolates Q1 so the table/procedure choice lives in one class) |
| Api | `AbsencesController` and `AbsenceRatesController` (thin: validate → service → response), registered in `Program.cs` |
| DB | Approval table DDL script under `database/scripts/` (Q6), unique `(employee_id, attend_date, group_key)`; `HRAPI_ABSENCE_PERIOD_RATE` DDL + seed script with today's rates |

## 7. Slices (each shippable and verified before the next)

| # | Slice | Done when |
|---|---|---|
| 1 ✅ | **Paged absence list** (built; Oracle verification pending) (`GET /api/hr/absences`; the `status` filter arrives with the approval table) | BDD `AbsenceListing.feature` + unit + integration tests green; for 16-Aug → 15-Sep 2026, men, the API returns **the same rows and total amount** as `Daily_Absent_Report_PlainSelect_16Aug_15Sep2026.sql` run directly |
| 2 | **Approval table + reject + approve (without deduction)**, with the rights gate | Table deployed on test; approve/reject change `status`; a repeat approve returns `already_decided`; a user without the right gets 403 |
| 3 | **Post month** (`post-month` + preview) through the Pay_S017 session path, plus the payroll-closed guard | Approving and posting the Sep-2026 men's list in UAT reproduces `Check_D33_Payroll_Teaching_Absent_15Sep2026.sql` (27 employees, one session row each, total ≈ 8,025; any per-employee difference is only the ±1 SAR rounding explained in Q8), and the bank-file view matches. Signed off by payroll on UAT; a forced failure leaves neither row behind |
| 4 | **Revoke** (if Q5) | Revoked lines are excluded from the next post-month; blocked after payroll close |
| 1b | **Rate configuration** (§5.5). Lookups ✅ built (`/grades`, `/positions`, `/periods`; rates interim in `AbsenceDeduction` config). Still to do: table + seed, endpoints, the absence query switched from literals to the rate lookup, and the page | With the seeded defaults, the slice-1 Sep-2026 result is unchanged to the halala. A grade-specific rate changes only that grade's amounts. A rate for a posted month is refused. A missing rate shows `NO_RATE` |
| 5 | **HrAuth.CleanArchitecture wrapper** — `IHrAbsenceProvider` port + REST client + controller that forwards the caller's JWT | Wrapper endpoints return the same results as HrApi; the wrapper has no business logic of its own |

## 8. Tests

- **BDD** (`HrApi.Tests/BDD/Features/`): `AbsenceListing.feature`,
  `AbsenceApproval.feature`. Scenarios: pending absences paged; approve adds a D33 line with the computed amount for the payroll month; approving twice is harmless; an absence fixed
  by attendance adjustment can't be approved; approval blocked after payroll close;
  reject records the reason and makes no deduction; a user without the right is refused.
- **Unit:** service outcomes per branch (mocked `IDeductionWriter`, rights service),
  validators (page bounds, range limits, id format, remarks length).
- **Integration:** against the SmartHCM **test** schema only. Commit, rollback on a
  deduction failure, the unique-key race, and paging totals.

## 9. Risks

- **Money moves.** A wrong quantity/amount or month deducts real salary. Slice 3 must be
  signed off by payroll on test data before going live.
- **Bypassing SmartHCM logic.** A raw insert may skip payroll's validations or
  audit. Prefer the SmartHCM procedure (Q1).
- **Stale lists.** Attendance may be corrected after the list loads. Re-checking at
  approve time (5.2 step 1) handles this.
- **Double deduction.** Handled by the unique key and per-id transaction.
- **Audit:** every decision stores the approver's employee id, timestamp and remarks. Logs
  carry ids only, never salary figures.
