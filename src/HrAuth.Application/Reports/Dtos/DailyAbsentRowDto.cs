namespace HrAuth.Application.Reports.Dtos;

/// <summary>
/// One row of the daily absent report: an employee / day / period-group with
/// no matching attendance. Periods 1–3 roll up into GRP_MORNING and 4–6 into
/// GRP_EVENING; any other period reports individually as P_&lt;id&gt;.
/// </summary>
public sealed record DailyAbsentRowDto
{
    public required string AttendDate { get; init; }       // yyyy-MM-dd
    public required string EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public string? Position { get; init; }
    public string? Department { get; init; }
    public required string GroupKey { get; init; }          // GRP_MORNING / GRP_EVENING / P_n
    public string? Periods { get; init; }                   // e.g. "P1+P2"
    public decimal RequiredHours { get; init; }
    public decimal PresentHours { get; init; }
    public decimal LeaveHours { get; init; }
    public string? AttendanceStatus { get; init; }
}
