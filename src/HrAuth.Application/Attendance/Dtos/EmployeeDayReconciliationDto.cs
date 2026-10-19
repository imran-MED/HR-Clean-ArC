namespace HrAuth.Application.Attendance.Dtos;

/// <summary>One employee's roster-vs-attendance check for a single day.</summary>
public sealed record EmployeeDayReconciliationDto(
    string EmployeeId,

    /// <summary>Employee display name (from the Employees table), if known.</summary>
    string? EmployeeName,

    /// <summary>Employee job title / position (from the Employees table), if known.</summary>
    string? Position,

    string Date,
    bool HasRoster,
    string? ShiftName,
    IReadOnlyList<PeriodAttendanceDto> Periods,
    int PunchCount,
    int MissingCount,

    /// <summary>Total late minutes across the day's periods.</summary>
    int LateMinutes,

    /// <summary>Total early-departure minutes across the day's periods.</summary>
    int EarlyDepartureMinutes,

    /// <summary>Number of periods with a suspected in/out direction issue.</summary>
    int SuspectCount = 0,

    /// <summary>True when an approved leave covers this day.</summary>
    bool OnLeave = false,

    /// <summary>The covering leave's name/type when <see cref="OnLeave"/>.</summary>
    string? LeaveName = null);
