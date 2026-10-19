using HrAuth.Application.Common;

namespace HrAuth.Application.Attendance;

/// <summary>
/// Port for posting attendance adjustments to the HR API
/// (<c>POST /api/hr/attendance</c> / <c>DELETE /api/hr/attendance/{id}</c>).
/// The caller's own bearer token is forwarded — the HR API enforces the
/// user's adjust-attendance rights.
/// </summary>
public interface IAttendanceAdjustmentProvider
{
    /// <summary>Create an attendance record for an employee/date.</summary>
    Task<Result<string>> AddAsync(
        string employeeId, DateTime attendanceDate, string? notes,
        string bearerToken, CancellationToken ct = default);

    /// <summary>Delete a previously created attendance record.</summary>
    Task<Result<string>> DeleteAsync(
        string attendanceId, string bearerToken, CancellationToken ct = default);
}
