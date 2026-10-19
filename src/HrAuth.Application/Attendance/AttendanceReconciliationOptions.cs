namespace HrAuth.Application.Attendance;

/// <summary>
/// Strongly-typed settings bound from the "AttendanceReconciliation"
/// configuration section.
/// </summary>
public sealed class AttendanceReconciliationOptions
{
    public const string SectionName = "AttendanceReconciliation";

    /// <summary>
    /// Default grace margin, in minutes. A punch within this many minutes of a
    /// period boundary still counts as on-time — applied to matching, late
    /// arrival and early departure. Used for any employee whose position is not
    /// listed in <see cref="GraceMinutesByPosition"/>. Defaults to 15. Values
    /// below zero are treated as zero.
    /// </summary>
    public int GraceMinutes { get; set; } = 15;

    /// <summary>
    /// Per-position grace overrides: a map of employee position (matched against
    /// <c>employees.Position</c>, case-insensitive) to grace minutes. An employee
    /// whose position appears here uses that value instead of
    /// <see cref="GraceMinutes"/>. Positions not listed fall back to the default.
    /// Values below zero are treated as zero.
    /// </summary>
    public Dictionary<string, int> GraceMinutesByPosition { get; set; } = new();

    /// <summary>
    /// The punch <c>Status</c> value that means "check-in" (دخول) in uploaded
    /// machine files. The opposite value is treated as "check-out" (خروج). Used
    /// to flag direction mistakes — e.g. a check-out punch sitting at a period's
    /// start. Defaults to 1 (the convention the upload screen displays).
    /// </summary>
    public int CheckInStatus { get; set; } = 1;

    /// <summary>
    /// Consecutive roster periods whose gap is at most this many minutes are
    /// merged into one "joined shift" block that takes a single check-in (at the
    /// block start) and a single check-out (at the block end) — e.g. the prayer
    /// shifts العصر/المغرب/العشاء. Periods further apart than this stay separate
    /// blocks, each needing its own in/out (e.g. man8's morning vs evening
    /// sessions, ~7.5h apart). Defaults to 120.
    /// </summary>
    public int JoinPeriodsMaxGapMinutes { get; set; } = 120;

    /// <summary>
    /// Maximum number of roster fetches issued to SmartHCM at once during a
    /// reconcile run. Higher = faster on large files but more load on the legacy
    /// service. Defaults to 24.
    /// </summary>
    public int MaxRosterConcurrency { get; set; } = 24;
}
