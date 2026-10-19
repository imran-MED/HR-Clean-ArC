namespace HrAuth.Application.Employees.Dtos;

/// <summary>Payload for POST /api/employees (single-employee add).</summary>
public sealed record AddEmployeeRequest(
    string EmployeeId,
    string DisplayName,
    string? UserName,
    string? Position = null,
    string? SupervisorEmployeeId = null);

/// <summary>
/// Returned by GET /api/employees, GET /api/employees/{id} and
/// POST /api/employees. <see cref="SupervisorName"/> is resolved from the
/// supervisor's directory row (null when unknown / not on record).
/// </summary>
public sealed record EmployeeDirectoryRow(
    string EmployeeId,
    string? DisplayName,
    string? UserName,
    IReadOnlyList<string> Roles,
    string? Position = null,
    string? SupervisorEmployeeId = null,
    string? SupervisorName = null);

/// <summary>Paged result for the directory tab.</summary>
public sealed record EmployeeDirectoryPage(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<EmployeeDirectoryRow> Items);
