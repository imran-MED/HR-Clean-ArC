namespace HrAuth.Application.Attendance.Dtos;

/// <summary>One parsed attendance punch, returned to the API caller.</summary>
public sealed record AttendanceRecordDto(
    int LineNumber,
    string EmployeeId,
    string RawEmployeeId,
    DateTime Timestamp,
    int Status,
    int Verify,
    int WorkCode,
    int Reserved);
