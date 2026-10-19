using HrAuth.Application.Common;
using HrAuth.Application.Reports.Dtos;

namespace HrAuth.Application.Reports;

/// <summary>
/// Port for the missing-punches detail report (teaching staff / admin).
/// Implemented against the HR API, which executes the report server-side
/// where the SmartHCM database is reachable.
/// </summary>
public interface IMissingPunchReportProvider
{
    /// <summary>
    /// Shift halves with exactly one (unpairable) punch over an inclusive
    /// date range, optionally filtered to one employee. The signed-in user's
    /// <paramref name="bearerToken"/> is forwarded to the HR API.
    /// </summary>
    Task<Result<IReadOnlyList<MissingPunchRowDto>>> GetAsync(
        DateOnly from, DateOnly to, string? employeeId, string? bearerToken,
        CancellationToken ct = default);
}
