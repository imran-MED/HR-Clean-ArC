using HrAuth.Application.Common;
using HrAuth.Domain.Rosters;

namespace HrAuth.Application.Rosters;

/// <summary>
/// Port for the external roster source (the legacy SmartHCM ASMX service).
/// Implemented in the Infrastructure layer.
/// </summary>
public interface IRosterProvider
{
    /// <summary>
    /// Fetch the shift roster for an employee on a date. A successful result
    /// with <see cref="Roster.IsEmpty"/> means no shift was assigned.
    /// </summary>
    Task<Result<Roster>> GetRosterAsync(
        string employeeId, DateOnly date, CancellationToken ct = default);
}
