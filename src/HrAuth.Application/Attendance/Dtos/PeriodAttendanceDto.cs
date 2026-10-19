namespace HrAuth.Application.Attendance.Dtos;

/// <summary>
/// A scheduled roster period checked against the uploaded punches, including
/// late-arrival and early-departure analysis (15-minute grace).
/// </summary>
public sealed record PeriodAttendanceDto(
    string Name,
    string StartTime,
    string EndTime,
    int PunchCount,
    bool IsMissing,

    /// <summary>Earliest matched punch ("HH:mm"), or null when missing.</summary>
    string? FirstPunch,

    /// <summary>Latest matched punch ("HH:mm"), or null when missing.</summary>
    string? LastPunch,

    /// <summary>Minutes the first punch is after the period start (0 within grace).</summary>
    int LateMinutes,
    bool IsLate,

    /// <summary>Minutes the last punch is before the period end (0 within grace).</summary>
    int EarlyDepartureMinutes,
    bool IsEarlyDeparture,

    /// <summary>Raw status of the period's check-in (first) punch, or null when missing.</summary>
    int? FirstPunchStatus = null,

    /// <summary>Raw status of the period's check-out (last) punch, or null when missing.</summary>
    int? LastPunchStatus = null,

    /// <summary>
    /// True when a punch's in/out direction looks wrong for its role — e.g. the
    /// check-in punch is marked as a check-out, or the period has a single punch.
    /// </summary>
    bool HasDirectionIssue = false,

    /// <summary>Human-readable description of the direction issue (Arabic), if any.</summary>
    string? DirectionNote = null,

    /// <summary>
    /// When this row is a joined shift block, the individual scheduled periods
    /// it merges (e.g. العصر / المغرب / العشاء). Null for a single-period block.
    /// </summary>
    IReadOnlyList<PeriodBreakupDto>? Breakups = null);
