using HrAuth.Application.Common;
using HrAuth.Application.Employees.Dtos;
using HrAuth.Application.Persistence;
using HrAuth.Domain.Attendance;

namespace HrAuth.Application.Employees;

/// <summary>
/// Imports the employee directory from the HR API into the local
/// employee-management directory: fetches every employee (id, name, position)
/// via <see cref="IHrApiDirectorySource"/> and bulk-upserts them. Existing
/// employees keep their UserName/Roles/LastLoginUtc; Position is only written
/// when the HR API reported one, so a blank never erases a stored value.
/// </summary>
public sealed class HrApiDirectoryImportService(
    IHrApiDirectorySource source,
    IEmployeeRepository employees) : IHrApiDirectoryImportService
{
    public async Task<Result<EmployeeDirectoryImportResult>> ImportAsync(
        string bearerToken, CancellationToken ct = default)
    {
        var fetched = await source.FetchAsync(bearerToken, ct);
        if (!fetched.IsSuccess)
            return Result<EmployeeDirectoryImportResult>.Failure(fetched.Error!);

        var entries = fetched.Value!;
        var warnings = new List<string>();

        // Last write wins on a repeated ID; IDs normalized to 8 digits.
        var rows = new Dictionary<string,
            (string Id, string Name, string? Position, string? SupervisorId)>(
            StringComparer.Ordinal);
        var skipped = 0;
        foreach (var e in entries)
        {
            if (string.IsNullOrWhiteSpace(e.EmployeeId)
                || string.IsNullOrWhiteSpace(e.Name))
            {
                skipped++;
                continue;
            }
            var id = EmployeeIdRules.Normalize(e.EmployeeId.Trim());
            rows[id] = (id, e.Name.Trim(),
                string.IsNullOrWhiteSpace(e.Position) ? null : e.Position.Trim(),
                null);
        }

        var imported = rows.Count > 0
            ? await employees.BulkUpsertDirectoryAsync(
                rows.Values.Select(r => (r.Id, r.Name, r.Position, r.SupervisorId)).ToList(),
                ct)
            : 0;

        if (skipped > 0)
            warnings.Add($"{skipped} entr(ies) had no ID or name and were skipped.");

        var total = await employees.CountAsync(ct);

        return Result<EmployeeDirectoryImportResult>.Success(
            new EmployeeDirectoryImportResult(
                FileName: "HR API directory",
                LinesParsed: entries.Count,
                RowsImported: imported,
                RowsSkipped: skipped,
                TotalEmployeesInDb: total,
                Warnings: warnings));
    }
}

/// <summary>Imports the HR API employee directory into the local one.</summary>
public interface IHrApiDirectoryImportService
{
    /// <summary>
    /// Fetch all employees from the HR API (authorized by the signed-in
    /// user's token) and upsert them into the local directory.
    /// </summary>
    Task<Result<EmployeeDirectoryImportResult>> ImportAsync(
        string bearerToken, CancellationToken ct = default);
}
