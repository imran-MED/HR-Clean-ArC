namespace HrAuth.Application.Rosters.Dtos;

/// <summary>One shift period, returned to the API caller.</summary>
public sealed record RosterPeriodDto(
    string Name,
    string StartTime,
    string EndTime,
    double RequiredHours,
    int SequenceNo);
