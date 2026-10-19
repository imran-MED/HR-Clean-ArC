using HrAuth.Application.Attendance.Dtos;
using HrAuth.Application.Common;

namespace HrAuth.Application.Attendance;

/// <summary>Use case: importing attendance machine export files.</summary>
public interface IAttendanceImportService
{
    /// <summary>
    /// Parse the raw text of an attendance machine export. Each non-blank
    /// line is
    /// <c>EmployeeId  Date  Time  Status  Verify  WorkCode  Reserved</c>;
    /// employee IDs are normalized to 8 digits. Malformed lines are skipped
    /// and reported in the result's <c>Errors</c>.
    /// </summary>
    Result<AttendanceImportResult> Parse(string fileName, string content);
}
