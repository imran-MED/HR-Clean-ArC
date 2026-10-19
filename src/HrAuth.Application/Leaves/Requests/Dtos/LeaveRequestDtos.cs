namespace HrAuth.Application.Leaves.Requests.Dtos;

/// <summary>A leave type the employee may apply for (SmartHCM <c>GetLeaveTypes</c>).</summary>
public sealed record LeaveTypeDto(
    /// <summary>Numeric leave-type id (<c>LEAVE_TYPE</c>).</summary>
    string Id,
    /// <summary>Localized description (<c>TYPE_DESC</c>).</summary>
    string Name,
    string? ShortName,
    /// <summary>Longest single stretch allowed, as SmartHCM reports it (<c>MAX_STRETCH</c>).</summary>
    string? MaxStretch,
    /// <summary>Minimum request size (<c>MIN_REQUIRED_LIMIT</c>).</summary>
    string? MinRequiredLimit,
    /// <summary>Every column SmartHCM returned, verbatim.</summary>
    IReadOnlyDictionary<string, string> Fields);

/// <summary>
/// Effective number of leave days from SmartHCM <c>GET_TO_DATE</c> (weekend /
/// holiday / leave-type aware — can differ from a naive date difference).
/// </summary>
public sealed record LeaveDaysDto(string Raw, double? Days);

/// <summary>The supervisor who must approve a leave (<c>Leave_getSupervisorName</c>).</summary>
public sealed record SupervisorDto(string Id, string Name);

/// <summary>A file sent with a leave request (e.g. a medical certificate).</summary>
public sealed record LeaveAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>A new leave request (SmartHCM <c>SaveLeave</c>).</summary>
public sealed record ApplyLeaveCommand(
    string LeaveTypeId,
    DateOnly FromDate,
    DateOnly ToDate,
    double NoOfDays,
    string? Remarks,
    /// <summary>Optional start time (hourly leave), e.g. <c>08:00</c>.</summary>
    string? FromTime,
    /// <summary>Optional end time (hourly leave).</summary>
    string? ToTime,
    IReadOnlyList<LeaveAttachment> Attachments);

/// <summary>Outcome of a SmartHCM write (<c>SaveLeave</c> / <c>Hrs_Leave_PerformAction</c>).</summary>
public sealed record LeaveWriteResultDto(
    bool Success,
    /// <summary>Server text (may be empty on success). Never raw exception text.</summary>
    string Message);

/// <summary>
/// One leave request as listed by SmartHCM <c>Hrs_GetPendingLeaves</c> — the
/// same list the Dawam app splits into "my leaves" and "approvals".
/// </summary>
public sealed record LeaveRequestRowDto(
    /// <summary>SmartHCM request number used by detail / perform-action calls.</summary>
    string? RflNo,
    string EmployeeId,
    string? EmployeeName,
    string? LeaveType,
    string? LeaveName,
    DateOnly? FromDate,
    DateOnly? ToDate,
    string? Status,
    /// <summary>P = pending, A = approved, R = rejected, C = cancelled/withdrawn.</summary>
    string? StatusId,
    double? DayHourRequested,
    /// <summary>'D' = days, 'H' = hours.</summary>
    string? DayType,
    bool IsPending,
    /// <summary>True when the row is the signed-in user's own request.</summary>
    bool IsOwn,
    /// <summary>
    /// True for leave types this supervisor only forwards to HR (his approval
    /// never finalizes the request) — shown as "Forward" instead of "Approve".
    /// </summary>
    bool RequiresHrApproval,
    /// <summary>Every column SmartHCM returned, verbatim.</summary>
    IReadOnlyDictionary<string, string> Fields);
