namespace HrAuth.Application.Rosters.Dtos;

/// <summary>An employee's roster for a date, returned to the API caller.</summary>
public sealed record RosterDto(
    string EmployeeId,
    string Date,
    bool HasRoster,
    string? ShiftName,
    string? ShiftStart,
    string? ShiftEnd,
    IReadOnlyList<RosterPeriodDto> Periods);
