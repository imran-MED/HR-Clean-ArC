using HrAuth.Application.Attendance.Dtos;

namespace HrAuth.Application.Persistence;

/// <summary>Persistence port for imported attendance punches.</summary>
public interface IAttendanceRepository
{
    /// <summary>Persist the punches parsed from an uploaded file.</summary>
    Task SaveRecordsAsync(
        string sourceFile,
        IReadOnlyList<AttendanceRecordDto> records,
        CancellationToken ct = default);
}
