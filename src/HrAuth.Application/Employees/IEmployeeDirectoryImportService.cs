using HrAuth.Application.Common;
using HrAuth.Application.Employees.Dtos;

namespace HrAuth.Application.Employees;

/// <summary>
/// Imports an employee directory (Employee ID → display name) from a
/// user-supplied CSV. Used to seed the <c>employees</c> table so that
/// reconciliation can show names for employees who have never signed in.
/// </summary>
public interface IEmployeeDirectoryImportService
{
    Task<Result<EmployeeDirectoryImportResult>> ImportAsync(
        string fileName, string content, CancellationToken ct = default);
}
