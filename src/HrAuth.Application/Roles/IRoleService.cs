using HrAuth.Application.Common;
using HrAuth.Application.Roles.Dtos;

namespace HrAuth.Application.Roles;

/// <summary>
/// Role catalog + per-employee role assignment service. Backs the Roles
/// page (Catalog + Assignment tabs).
/// </summary>
public interface IRoleService
{
    // ── Catalog ──
    Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct = default);
    Task<Result<RoleDto>> AddRoleAsync(RoleUpsertRequest request, CancellationToken ct = default);
    Task<Result<RoleDto>> UpdateRoleAsync(string name, RoleUpsertRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteRoleAsync(string name, CancellationToken ct = default);

    // ── Assignment ──
    Task<EmployeeRolesPage> ListEmployeesAsync(
        string? search, int page, int pageSize, CancellationToken ct = default);

    Task<Result<EmployeeWithRolesDto>> SetEmployeeRolesAsync(
        string employeeId, IReadOnlyList<string> roles, CancellationToken ct = default);
}
