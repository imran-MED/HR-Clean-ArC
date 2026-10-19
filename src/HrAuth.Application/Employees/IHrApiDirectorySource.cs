using HrAuth.Application.Common;

namespace HrAuth.Application.Employees;

/// <summary>
/// Port for the HR API's bulk employee directory
/// (<c>GET /api/hr/employee/directory</c>). The signed-in user's bearer token
/// is forwarded — the HR API enforces its own directory-read authorization.
/// </summary>
public interface IHrApiDirectorySource
{
    /// <summary>Fetch every employee (id, display name, position).</summary>
    Task<Result<IReadOnlyList<HrApiDirectoryEmployee>>> FetchAsync(
        string bearerToken, CancellationToken ct = default);
}

/// <summary>One directory entry as reported by the HR API.</summary>
public sealed record HrApiDirectoryEmployee(
    string EmployeeId, string Name, string? Position);
