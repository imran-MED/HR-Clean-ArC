using HrAuth.Application.Common;
using HrAuth.Application.Employees.Dtos;

namespace HrAuth.Application.Employees;

/// <summary>
/// Read + single-add operations for the employee directory. Bulk CSV import
/// lives on <see cref="IEmployeeDirectoryImportService"/>.
/// </summary>
public interface IEmployeeService
{
    Task<EmployeeDirectoryPage> ListAsync(
        string? search, int page, int pageSize, string? position,
        CancellationToken ct = default);

    /// <summary>Distinct employee positions, sorted — for the directory filter.</summary>
    Task<IReadOnlyList<string>> ListPositionsAsync(CancellationToken ct = default);

    /// <summary>Fetch a single employee (with resolved supervisor name).</summary>
    Task<Result<EmployeeDirectoryRow>> GetAsync(
        string employeeId, CancellationToken ct = default);

    /// <summary>
    /// Pull the employee's position + supervisor from SmartHCM and persist
    /// them, returning the updated directory row. Fails if the employee is
    /// not in the local directory or SmartHCM could not be reached.
    /// </summary>
    Task<Result<EmployeeDirectoryRow>> SyncFromSmartHcmAsync(
        string employeeId, CancellationToken ct = default);

    Task<Result<EmployeeDirectoryRow>> AddAsync(
        AddEmployeeRequest request, CancellationToken ct = default);
}
