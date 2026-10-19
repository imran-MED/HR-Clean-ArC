using HrAuth.Application.Attendance.Dtos;
using HrAuth.Application.Common;

namespace HrAuth.Application.Attendance;

/// <summary>
/// Port for fetching SmartHCM's computed daily attendance summary for a single
/// employee — the <c>Attendance_GetAttendance</c> ASMX operation the Dawam
/// mobile app uses. Self-authenticating per the employee's own password.
/// </summary>
public interface IAttendanceSummaryProvider
{
    /// <summary>
    /// Fetch the day-by-day attendance summary for <paramref name="employeeId"/>
    /// between <paramref name="from"/> and <paramref name="to"/>.
    /// <paramref name="plaintextPassword"/> is that employee's own password
    /// (hashed before transmission). Returns the parsed day rows.
    /// </summary>
    Task<Result<IReadOnlyList<AttendanceDaySummaryDto>>> GetAsync(
        string employeeId,
        string plaintextPassword,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default);
}
