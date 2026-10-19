namespace HrAuth.Application.Attendance.Dtos;

/// <summary>Outcome of importing an attendance machine export file.</summary>
public sealed record AttendanceImportResult(
    string FileName,
    int TotalLines,
    int ImportedCount,
    int SkippedCount,
    IReadOnlyList<AttendanceRecordDto> Records,
    IReadOnlyList<string> Errors);
