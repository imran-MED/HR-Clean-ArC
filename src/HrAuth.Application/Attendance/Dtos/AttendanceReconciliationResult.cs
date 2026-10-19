namespace HrAuth.Application.Attendance.Dtos;

/// <summary>
/// An attendance import together with its reconciliation against employee
/// rosters: which scheduled periods have a matching punch, which are missing,
/// and the total late-arrival / early-departure minutes.
/// </summary>
public sealed record AttendanceReconciliationResult(
    string FileName,
    int TotalLines,
    int ImportedCount,
    int SkippedCount,
    IReadOnlyList<AttendanceRecordDto> Records,
    IReadOnlyList<string> Errors,
    IReadOnlyList<EmployeeDayReconciliationDto> Days,
    int TotalMissingPeriods,
    int EmployeesWithoutRoster,
    int TotalLateMinutes,
    int TotalEarlyDepartureMinutes,

    /// <summary>Total periods flagged with a suspected in/out direction issue.</summary>
    int TotalDirectionIssues = 0,

    /// <summary>Number of employee-days covered by an approved leave.</summary>
    int TotalOnLeave = 0);
