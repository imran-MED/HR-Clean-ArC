namespace HrAuth.Domain.Rosters;

/// <summary>An employee's shift assignment for a single day.</summary>
public sealed class Roster
{
    /// <summary>Shift name, derived from the first period (e.g. "man8").</summary>
    public required string ShiftName { get; init; }

    /// <summary>Start time of the first period.</summary>
    public required string ShiftStart { get; init; }

    /// <summary>End time of the last period.</summary>
    public required string ShiftEnd { get; init; }

    public required IReadOnlyList<RosterPeriod> Periods { get; init; }

    /// <summary>True when no periods are assigned for the day.</summary>
    public bool IsEmpty => Periods.Count == 0;

    /// <summary>A roster with no shift assigned.</summary>
    public static Roster Empty => new()
    {
        ShiftName = string.Empty,
        ShiftStart = string.Empty,
        ShiftEnd = string.Empty,
        Periods = [],
    };
}
