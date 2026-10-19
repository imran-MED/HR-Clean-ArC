using HrAuth.Application.Common;
using HrAuth.Application.Rosters.Dtos;

namespace HrAuth.Application.Rosters;

/// <summary>Use case: retrieving an employee's shift roster.</summary>
public interface IRosterService
{
    /// <summary>
    /// Fetch an employee's shift roster for a date (defaults to today). The
    /// employee ID is normalized to 8 digits before lookup.
    /// </summary>
    Task<Result<RosterDto>> GetRosterAsync(
        string employeeId, DateOnly? date, CancellationToken ct = default);
}
