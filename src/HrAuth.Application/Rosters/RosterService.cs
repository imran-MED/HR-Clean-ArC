using HrAuth.Application.Common;
using HrAuth.Application.Rosters.Dtos;
using HrAuth.Domain.Attendance; // EmployeeIdRules
using HrAuth.Domain.Rosters;

namespace HrAuth.Application.Rosters;

/// <summary>
/// Coordinates the roster use case: normalizes the employee ID, delegates to
/// the <see cref="IRosterProvider"/> port, and maps the domain
/// <see cref="Roster"/> onto a <see cref="RosterDto"/>.
/// </summary>
public sealed class RosterService(IRosterProvider provider) : IRosterService
{
    public async Task<Result<RosterDto>> GetRosterAsync(
        string employeeId, DateOnly? date, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<RosterDto>.Failure("Employee ID is required.");

        // The roster API only matches 8-digit IDs — reuse the domain rule.
        var normalizedId = EmployeeIdRules.Normalize(employeeId);
        var day = date ?? DateOnly.FromDateTime(DateTime.Now);
        var dateText = day.ToString("yyyy-MM-dd");

        var result = await provider.GetRosterAsync(normalizedId, day, ct);
        if (!result.IsSuccess || result.Value is null)
            return Result<RosterDto>.Failure(result.Error, result.Errors);

        var roster = result.Value;
        if (roster.IsEmpty)
        {
            return Result<RosterDto>.Success(new RosterDto(
                normalizedId, dateText, HasRoster: false,
                ShiftName: null, ShiftStart: null, ShiftEnd: null,
                Periods: []));
        }

        var periods = roster.Periods
            .Select(p => new RosterPeriodDto(
                p.Name, p.StartTime, p.EndTime, p.RequiredHours, p.SequenceNo))
            .ToList();

        return Result<RosterDto>.Success(new RosterDto(
            normalizedId, dateText, HasRoster: true,
            roster.ShiftName, roster.ShiftStart, roster.ShiftEnd, periods));
    }
}
