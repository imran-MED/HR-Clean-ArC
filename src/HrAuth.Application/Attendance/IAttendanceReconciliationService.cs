using HrAuth.Application.Attendance.Dtos;
using HrAuth.Application.Common;

namespace HrAuth.Application.Attendance;

/// <summary>
/// Use case: import an attendance file and reconcile it against employee
/// rosters, reporting scheduled periods with no matching punch.
/// </summary>
public interface IAttendanceReconciliationService
{
    Task<Result<AttendanceReconciliationResult>> ReconcileAsync(
        string fileName, string content, CancellationToken ct = default);
}
