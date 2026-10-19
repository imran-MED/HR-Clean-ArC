using HrAuth.Application.Common;
using HrAuth.Application.Reports.Dtos;

namespace HrAuth.Application.Reports;

/// <summary>
/// Port for the daily absent report (teaching staff / admin). Implemented in
/// Infrastructure against the SmartHCM Oracle schema.
/// </summary>
public interface IDailyAbsentReportProvider
{
    /// <summary>
    /// Absences (attendance status not P/L) for the configured positions and
    /// employee type, grouped per employee / day / period-group, over an
    /// inclusive date range. Optionally filtered to one employee.
    /// <paramref name="bearerToken"/> is the signed-in user's token, forwarded
    /// by implementations that call the HR API; the direct-Oracle
    /// implementation ignores it.
    /// </summary>
    Task<Result<IReadOnlyList<DailyAbsentRowDto>>> GetAsync(
        DateOnly from, DateOnly to, string? employeeId, string? bearerToken,
        CancellationToken ct = default);
}
