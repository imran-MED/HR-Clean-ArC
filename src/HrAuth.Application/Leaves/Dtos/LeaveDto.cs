namespace HrAuth.Application.Leaves.Dtos;

/// <summary>
/// One employee leave record fetched from SmartHCM (<c>Hrs_GetPendingLeaves</c>).
/// </summary>
public sealed record LeaveDto(
    string EmployeeId,
    string? EmployeeName,

    /// <summary>Numeric leave-type id (<c>LEAVE_TYPE</c>).</summary>
    string? LeaveType,

    /// <summary>Localized leave-type name (<c>LEAVE_NAME</c>).</summary>
    string? LeaveName,

    /// <summary>First day of the leave (<c>DATE_FROM</c>, normalized).</summary>
    DateOnly FromDate,

    /// <summary>Last day of the leave (<c>DATE_TO</c>, normalized).</summary>
    DateOnly ToDate,

    /// <summary>Localized status text (<c>STATUS</c>).</summary>
    string? Status,

    /// <summary>Stable status code (<c>STATUS_ID</c>): P/A/R/C.</summary>
    string? StatusId,

    /// <summary>Requested days or hours (<c>DAY_HOUR_REQUESTED</c>).</summary>
    double? DayHourRequested,

    /// <summary>'D' = days, 'H' = hours (<c>LEAVE_DAY_TYPE</c>).</summary>
    string? DayType)
{
    /// <summary>True when the leave is approved (status code 'A').</summary>
    public bool IsApproved =>
        string.Equals(StatusId?.Trim(), "A", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when <paramref name="date"/> falls within the leave span.</summary>
    public bool Covers(DateOnly date) => date >= FromDate && date <= ToDate;
}
