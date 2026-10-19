namespace HrAuth.Application.Attendance.Dtos;

/// <summary>
/// One scheduled sub-period inside a joined shift block (e.g. العصر / المغرب /
/// العشاء), shown as a breakup. Display-only — the block as a whole carries the
/// single check-in / check-out.
/// </summary>
public sealed record PeriodBreakupDto(
    string Name,
    string StartTime,
    string EndTime);
