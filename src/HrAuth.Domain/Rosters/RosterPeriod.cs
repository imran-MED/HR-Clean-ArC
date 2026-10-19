namespace HrAuth.Domain.Rosters;

/// <summary>
/// One time-window inside a daily shift
/// (e.g. <c>man8-b1  05:00 → 09:00</c>).
/// </summary>
public sealed class RosterPeriod
{
    public required string Name { get; init; }
    public required string StartTime { get; init; }
    public required string EndTime { get; init; }

    /// <summary>Hours the employee is expected to work in this window.</summary>
    public double RequiredHours { get; init; }

    /// <summary>Ordering key — periods read in ascending sequence.</summary>
    public int SequenceNo { get; init; }
}
