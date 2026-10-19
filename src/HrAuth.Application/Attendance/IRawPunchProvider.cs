using HrAuth.Application.Common;

namespace HrAuth.Application.Attendance;

/// <summary>
/// Port for pulling raw attendance punches from an external source over a date
/// range (e.g. SmartHCM's <c>TOM_MACHINE_DATA</c> via direct Oracle). The
/// punches are returned already formatted as the attendance machine-export text
/// the importer understands (<c>EmployeeId Date Time Status Verify WorkCode
/// Reserved</c>), so a fetch reuses the exact same reconciliation pipeline as a
/// file upload.
/// </summary>
public interface IRawPunchProvider
{
    /// <summary>
    /// Fetch raw punches in <paramref name="from"/>..<paramref name="to"/>
    /// (inclusive). <paramref name="employeeId"/> null/empty = all employees.
    /// On success returns the synthesized export text (may be empty when no
    /// punches exist in the window).
    /// </summary>
    Task<Result<string>> GetPunchFileAsync(
        DateOnly from, DateOnly to, string? employeeId, CancellationToken ct = default);
}
