namespace HrAuth.Application.Attendance.Dtos;

/// <summary>
/// One day of SmartHCM's <b>already-computed</b> attendance summary, as returned
/// by the ASMX <c>Attendance_GetAttendance</c> operation (the same source the
/// Dawam mobile app's attendance log renders). This is a daily roll-up — not raw
/// punches — so it feeds the missing / late-early / absent views, not the
/// direction/duplicate review.
/// </summary>
public sealed record AttendanceDaySummaryDto(
    string Date,
    string? DateHijri,
    string? DayName,
    string? TimeIn,
    string? TimeOut,
    string? Shift,
    string? Location,
    string? Remarks,
    string? RequiredHours,
    string? PresentHours,
    bool IsMissing);
