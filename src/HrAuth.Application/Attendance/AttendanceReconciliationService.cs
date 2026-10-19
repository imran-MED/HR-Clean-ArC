using HrAuth.Application.Attendance.Dtos;
using HrAuth.Application.Common;
using HrAuth.Application.Leaves;
using HrAuth.Application.Leaves.Dtos;
using HrAuth.Application.Persistence;
using HrAuth.Application.Rosters;
using HrAuth.Domain.Attendance;
using HrAuth.Domain.Rosters;
using Microsoft.Extensions.Options;

namespace HrAuth.Application.Attendance;

/// <summary>
/// Orchestrates an attendance upload against employee rosters: parses the
/// file, checks each scheduled period for a matching punch (flagging missing
/// periods, late arrivals and early departures against a 15-minute grace),
/// marks days covered by an approved leave, enriches each employee with their
/// name, and persists the result.
/// </summary>
public sealed class AttendanceReconciliationService(
    IAttendanceImportService importer,
    IRosterProvider rosterProvider,
    IEmployeeRepository employees,
    IAttendanceRepository attendanceRepository,
    IReconciliationRepository reconciliationRepository,
    ILeaveProvider leaveProvider,
    IOptions<AttendanceReconciliationOptions> options)
    : IAttendanceReconciliationService
{
    /// <summary>
    /// Default grace margin — a punch within this margin of a period boundary
    /// still counts as on-time (for matching, lateness and early departure).
    /// Applies to any employee whose position has no specific override.
    /// Configured via <see cref="AttendanceReconciliationOptions.GraceMinutes"/>.
    /// </summary>
    private readonly TimeSpan _defaultGrace =
        TimeSpan.FromMinutes(Math.Max(0, options.Value.GraceMinutes));

    /// <summary>
    /// Per-position grace overrides (position → margin), matched
    /// case-insensitively. Built from
    /// <see cref="AttendanceReconciliationOptions.GraceMinutesByPosition"/>.
    /// </summary>
    private readonly IReadOnlyDictionary<string, TimeSpan> _graceByPosition =
        (options.Value.GraceMinutesByPosition ?? [])
            .ToDictionary(
                kv => kv.Key,
                kv => TimeSpan.FromMinutes(Math.Max(0, kv.Value)),
                StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Punch status value that means "check-in"; the opposite means "check-out".
    /// Used to flag in/out direction mistakes.
    /// </summary>
    private readonly int _checkInStatus = options.Value.CheckInStatus;

    /// <summary>
    /// Maximum gap between consecutive roster periods that are still merged into
    /// one joined-shift block (single check-in / check-out).
    /// </summary>
    private readonly TimeSpan _joinMaxGap =
        TimeSpan.FromMinutes(Math.Max(0, options.Value.JoinPeriodsMaxGapMinutes));

    /// <summary>Max concurrent roster fetches during a reconcile run.</summary>
    private readonly int _maxRosterConcurrency =
        Math.Max(1, options.Value.MaxRosterConcurrency);

    /// <summary>
    /// Resolve the grace margin for an employee: their position-specific
    /// override if one is configured, otherwise the default.
    /// </summary>
    private TimeSpan ResolveGrace(string? position)
        => position is not null && _graceByPosition.TryGetValue(position, out var g)
            ? g
            : _defaultGrace;

    public async Task<Result<AttendanceReconciliationResult>> ReconcileAsync(
        string fileName, string content, CancellationToken ct = default)
    {
        var parsed = importer.Parse(fileName, content);
        if (!parsed.IsSuccess || parsed.Value is null)
            return Result<AttendanceReconciliationResult>.Failure(
                parsed.Error, parsed.Errors);

        var import = parsed.Value;

        // Group punches by employee + calendar day, then reconcile each group.
        var groups = import.Records
            .GroupBy(r => (r.EmployeeId, Date: DateOnly.FromDateTime(r.Timestamp)))
            .ToList();

        // Resolve each employee's position up front so the grace margin can be
        // chosen per position before the day reconciliations run.
        var employeeIds = groups.Select(g => g.Key.EmployeeId).Distinct().ToList();
        var positions = await SafeGetPositionsAsync(employeeIds, ct);

        // Approved leaves (fetched in bulk via the admin credential, cached) so a
        // day covered by an approved leave is marked on-leave, not missing.
        var leavesByEmployee = await SafeGetApprovedLeavesAsync(ct);

        // Bound concurrency: a large file can have thousands of employee/day
        // groups, each needing a roster fetch. Firing them all at once would
        // overwhelm SmartHCM and the connection pool, so cap parallelism.
        using var gate = new SemaphoreSlim(_maxRosterConcurrency);
        var dayTasks = groups.Select(async g =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var leave = FindLeave(leavesByEmployee, g.Key.EmployeeId, g.Key.Date);
                return await ReconcileDayAsync(g.Key.EmployeeId, g.Key.Date, g.ToList(),
                    ResolveGrace(positions.GetValueOrDefault(g.Key.EmployeeId)), leave, ct);
            }
            finally
            {
                gate.Release();
            }
        });
        var days = (await Task.WhenAll(dayTasks))
            .OrderBy(d => d.EmployeeId, StringComparer.Ordinal)
            .ThenBy(d => d.Date, StringComparer.Ordinal)
            .ToList();

        // Enrich each day with the employee name and position from the
        // Employees table.
        var names = await SafeGetNamesAsync(
            days.Select(d => d.EmployeeId).Distinct().ToList(), ct);
        days = days
            .Select(d => d with
            {
                EmployeeName = names.GetValueOrDefault(d.EmployeeId),
                Position = positions.GetValueOrDefault(d.EmployeeId),
            })
            .ToList();

        var result = new AttendanceReconciliationResult(
            FileName: import.FileName,
            TotalLines: import.TotalLines,
            ImportedCount: import.ImportedCount,
            SkippedCount: import.SkippedCount,
            Records: import.Records,
            Errors: import.Errors,
            Days: days,
            TotalMissingPeriods: days.Sum(d => d.MissingCount),
            EmployeesWithoutRoster: days.Count(d => !d.HasRoster && !d.OnLeave),
            TotalLateMinutes: days.Sum(d => d.LateMinutes),
            TotalEarlyDepartureMinutes: days.Sum(d => d.EarlyDepartureMinutes),
            TotalDirectionIssues: days.Sum(d => d.SuspectCount),
            TotalOnLeave: days.Count(d => d.OnLeave));

        // Persist the import and the run (best-effort — never blocks the upload).
        await SafePersistAsync(import, result, ct);

        return Result<AttendanceReconciliationResult>.Success(result);
    }

    private async Task<IReadOnlyDictionary<string, string>> SafeGetNamesAsync(
        IReadOnlyCollection<string> employeeIds, CancellationToken ct)
    {
        try
        {
            return await employees.GetNamesAsync(employeeIds, ct);
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> SafeGetPositionsAsync(
        IReadOnlyCollection<string> employeeIds, CancellationToken ct)
    {
        try
        {
            return await employees.GetPositionsAsync(employeeIds, ct);
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Bulk-fetch approved leaves (admin credential, cached) and index them by
    /// normalized employee id. Best-effort: any failure yields no leaves.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, List<LeaveDto>>>
        SafeGetApprovedLeavesAsync(CancellationToken ct)
    {
        var map = new Dictionary<string, List<LeaveDto>>(StringComparer.Ordinal);
        try
        {
            var result = await leaveProvider.GetAllLeavesAsync(ct);
            if (!result.IsSuccess || result.Value is null)
                return map;

            foreach (var leave in result.Value.Where(l => l.IsApproved))
            {
                var key = EmployeeIdRules.Normalize(leave.EmployeeId);
                if (!map.TryGetValue(key, out var list))
                    map[key] = list = [];
                list.Add(leave);
            }
        }
        catch
        {
            // ignore — leaves are an enrichment, not required for reconciliation
        }
        return map;
    }

    private static LeaveDto? FindLeave(
        IReadOnlyDictionary<string, List<LeaveDto>> leaves,
        string employeeId, DateOnly date)
        => leaves.TryGetValue(employeeId, out var list)
            ? list.FirstOrDefault(l => l.Covers(date))
            : null;

    private async Task SafePersistAsync(
        AttendanceImportResult import, AttendanceReconciliationResult result,
        CancellationToken ct)
    {
        try
        {
            await attendanceRepository.SaveRecordsAsync(
                import.FileName, import.Records, ct);
            await reconciliationRepository.SaveRunAsync(result, ct);
        }
        catch
        {
            // The database is optional — ignore persistence failures.
        }
    }

    private async Task<EmployeeDayReconciliationDto> ReconcileDayAsync(
        string employeeId, DateOnly date, List<AttendanceRecordDto> punches,
        TimeSpan grace, LeaveDto? leave, CancellationToken ct)
    {
        var dateText = date.ToString("yyyy-MM-dd");

        // A day covered by an approved leave is not an absence — skip roster
        // reconciliation and mark it on-leave (no missing/late flags).
        if (leave is not null)
        {
            return new EmployeeDayReconciliationDto(
                employeeId, EmployeeName: null, Position: null, dateText,
                HasRoster: false, ShiftName: null,
                Periods: [], PunchCount: punches.Count, MissingCount: 0,
                LateMinutes: 0, EarlyDepartureMinutes: 0, SuspectCount: 0,
                OnLeave: true, LeaveName: leave.LeaveName ?? leave.Status);
        }

        var rosterResult = await rosterProvider.GetRosterAsync(employeeId, date, ct);
        var roster = rosterResult.IsSuccess ? rosterResult.Value : null;

        if (roster is null || roster.IsEmpty)
        {
            return new EmployeeDayReconciliationDto(
                employeeId, EmployeeName: null, Position: null, dateText,
                HasRoster: false, ShiftName: null,
                Periods: [], PunchCount: punches.Count, MissingCount: 0,
                LateMinutes: 0, EarlyDepartureMinutes: 0);
        }

        // Merge consecutive periods into joined-shift blocks (one check-in /
        // check-out per block); far-apart sessions stay separate blocks.
        var periods = MergeIntoBlocks(roster.Periods, _joinMaxGap)
            .Select(block => BuildBlock(block, date, punches, grace, _checkInStatus))
            .ToList();

        return new EmployeeDayReconciliationDto(
            employeeId, EmployeeName: null, Position: null, dateText,
            HasRoster: true, ShiftName: roster.ShiftName,
            Periods: periods,
            PunchCount: punches.Count,
            MissingCount: periods.Count(p => p.IsMissing),
            LateMinutes: periods.Sum(p => p.LateMinutes),
            EarlyDepartureMinutes: periods.Sum(p => p.EarlyDepartureMinutes),
            SuspectCount: periods.Count(p => p.HasDirectionIssue));
    }

    /// <summary>
    /// Merge a roster's periods into joined-shift blocks: consecutive periods
    /// (ordered by start time) whose gap is at most <paramref name="maxGap"/>
    /// are grouped together, so a continuous shift like العصر/المغرب/العشاء
    /// becomes one block. Periods further apart (e.g. man8's morning vs evening)
    /// stay in separate blocks. Periods with unreadable times stand alone.
    /// </summary>
    private static List<List<RosterPeriod>> MergeIntoBlocks(
        IReadOnlyList<RosterPeriod> periods, TimeSpan maxGap)
    {
        var ordered = periods
            .Select(p => (
                Period: p,
                Start: TimeOnly.TryParse(p.StartTime, out var s) ? s : (TimeOnly?)null,
                End: TimeOnly.TryParse(p.EndTime, out var e) ? e : (TimeOnly?)null))
            .OrderBy(x => x.Start ?? TimeOnly.MinValue)
            .ToList();

        var blocks = new List<List<RosterPeriod>>();
        List<RosterPeriod>? current = null;
        TimeOnly? currentEnd = null;

        foreach (var x in ordered)
        {
            var canExtend = current is not null && currentEnd is not null &&
                            x.Start is not null &&
                            x.Start.Value - currentEnd.Value <= maxGap;
            if (canExtend)
            {
                current!.Add(x.Period);
                if (x.End is not null && x.End.Value > currentEnd!.Value)
                    currentEnd = x.End;
            }
            else
            {
                current = [x.Period];
                blocks.Add(current);
                currentEnd = x.End;
            }
        }

        return blocks;
    }

    /// <summary>
    /// Reconcile one joined-shift block against the day's punches: a single
    /// check-in at the block start and a single check-out at the block end,
    /// with late arrival / early departure forgiven within the grace margin and
    /// in/out direction mistakes flagged. The merged periods are returned as
    /// breakups for display.
    /// </summary>
    private static PeriodAttendanceDto BuildBlock(
        List<RosterPeriod> block, DateOnly date, List<AttendanceRecordDto> punches,
        TimeSpan grace, int checkInStatus)
    {
        var name = string.Join(" · ", block.Select(p => p.Name));
        var startTime = block[0].StartTime;
        var endTime = block[^1].EndTime;
        IReadOnlyList<PeriodBreakupDto>? breakups = block.Count > 1
            ? block.Select(p => new PeriodBreakupDto(p.Name, p.StartTime, p.EndTime)).ToList()
            : null;

        if (!TimeOnly.TryParse(startTime, out var start) ||
            !TimeOnly.TryParse(endTime, out var end))
        {
            // Block times unreadable — cannot reconcile.
            return new PeriodAttendanceDto(
                name, startTime, endTime,
                PunchCount: 0, IsMissing: false,
                FirstPunch: null, LastPunch: null,
                LateMinutes: 0, IsLate: false,
                EarlyDepartureMinutes: 0, IsEarlyDeparture: false,
                Breakups: breakups);
        }

        var midnight = date.ToDateTime(TimeOnly.MinValue);
        var startAt = midnight.Add(start.ToTimeSpan());
        var endAt = midnight.Add(end.ToTimeSpan());
        if (end <= start)
            endAt = endAt.AddDays(1); // block spills past midnight

        var matched = punches
            .Where(p => p.Timestamp >= startAt - grace && p.Timestamp <= endAt + grace)
            .OrderBy(p => p.Timestamp)
            .ToList();

        if (matched.Count == 0)
        {
            return new PeriodAttendanceDto(
                name, startTime, endTime,
                PunchCount: 0, IsMissing: true,
                FirstPunch: null, LastPunch: null,
                LateMinutes: 0, IsLate: false,
                EarlyDepartureMinutes: 0, IsEarlyDeparture: false,
                Breakups: breakups);
        }

        var graceMinutes = (int)grace.TotalMinutes;

        // Treat the earliest punch as the arrival (check-in) and the latest as
        // the departure (check-out). A departure only exists when there is a
        // DISTINCT second punch — a lone punch is an arrival with a missing
        // check-out, never both. This stops a single punch being counted as its
        // own check-out and producing a bogus early departure.
        var arrival = matched[0];
        var hasDeparture = matched.Count >= 2;
        var departure = hasDeparture ? matched[^1] : null;

        // Late: arrival after the start, forgiven within grace.
        var lateRaw = (int)Math.Round((arrival.Timestamp - startAt).TotalMinutes);
        var lateMinutes = lateRaw > graceMinutes ? lateRaw : 0;

        // Early departure: only when a real (distinct) check-out punch exists.
        var earlyMinutes = 0;
        if (departure is not null)
        {
            var earlyRaw = (int)Math.Round((endAt - departure.Timestamp).TotalMinutes);
            earlyMinutes = earlyRaw > graceMinutes ? earlyRaw : 0;
        }

        // Issues: a single punch means the check-out is missing; with two or more
        // punches use the in/out status (when meaningful) to flag duplicates and
        // wrong-direction punches.
        var issues = new List<string>();
        if (!hasDeparture)
        {
            issues.Add("بصمة واحدة فقط — لا توجد بصمة خروج");
        }
        else
        {
            var checkIns = matched.Count(m => m.Status == checkInStatus);
            var checkOuts = matched.Count - checkIns;

            var duplicateIn = false;
            var duplicateOut = false;
            for (var k = 1; k < matched.Count; k++)
            {
                var prevIsIn = matched[k - 1].Status == checkInStatus;
                var currIsIn = matched[k].Status == checkInStatus;
                if (prevIsIn && currIsIn) duplicateIn = true;
                else if (!prevIsIn && !currIsIn) duplicateOut = true;
            }
            if (duplicateIn) issues.Add("تكرار بصمة دخول");
            if (duplicateOut) issues.Add("تكرار بصمة خروج");

            if (checkIns > 0 && checkOuts == 0) issues.Add("لا توجد بصمة خروج");
            else if (checkOuts > 0 && checkIns == 0) issues.Add("لا توجد بصمة دخول");
        }

        var hasDirectionIssue = issues.Count > 0;
        var directionNote = hasDirectionIssue ? string.Join(" · ", issues) : null;

        return new PeriodAttendanceDto(
            name, startTime, endTime,
            PunchCount: matched.Count, IsMissing: false,
            FirstPunch: arrival.Timestamp.ToString("HH:mm"),
            LastPunch: departure?.Timestamp.ToString("HH:mm"),
            LateMinutes: lateMinutes, IsLate: lateMinutes > 0,
            EarlyDepartureMinutes: earlyMinutes,
            IsEarlyDeparture: earlyMinutes > 0,
            FirstPunchStatus: arrival.Status, LastPunchStatus: departure?.Status,
            HasDirectionIssue: hasDirectionIssue, DirectionNote: directionNote,
            Breakups: breakups);
    }
}
