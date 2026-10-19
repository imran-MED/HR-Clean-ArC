using HrAuth.Application.Common;
using HrAuth.Application.Persistence;
using HrAuth.Application.Roles.Dtos;

namespace HrAuth.Application.Roles;

/// <summary>
/// Default <see cref="IRoleService"/> backed by <see cref="IRoleRepository"/>
/// (catalog) and <see cref="IEmployeeRepository"/> (assignment).
/// </summary>
public sealed class RoleService(
    IRoleRepository roles, IEmployeeRepository employees) : IRoleService
{
    private const int MaxNameLength = 64;
    private const int MaxDescriptionLength = 256;
    private const int MaxPageSize = 200;

    // ── Catalog ──

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct = default)
    {
        var catalog = await roles.ListAsync(ct);
        var counts = await roles.CountAssignmentsAsync(ct);
        return catalog
            .Select(r => new RoleDto(
                r.Name,
                r.Description,
                counts.TryGetValue(r.Name, out var c) ? c : 0))
            .ToList();
    }

    public async Task<Result<RoleDto>> AddRoleAsync(
        RoleUpsertRequest request, CancellationToken ct = default)
    {
        var nameError = ValidateName(request.Name);
        if (nameError is not null) return Result<RoleDto>.Failure(nameError);
        var name = request.Name.Trim();

        var description = TrimDescription(request.Description);
        var descError = ValidateDescription(description);
        if (descError is not null) return Result<RoleDto>.Failure(descError);

        var added = await roles.AddAsync(name, description, ct);
        if (!added)
            return Result<RoleDto>.Failure($"Role '{name}' already exists.");

        return Result<RoleDto>.Success(new RoleDto(name, description, 0));
    }

    public async Task<Result<RoleDto>> UpdateRoleAsync(
        string name, RoleUpsertRequest request, CancellationToken ct = default)
    {
        var nameError = ValidateName(name);
        if (nameError is not null) return Result<RoleDto>.Failure(nameError);
        name = name.Trim();

        var description = TrimDescription(request.Description);
        var descError = ValidateDescription(description);
        if (descError is not null) return Result<RoleDto>.Failure(descError);

        var updated = await roles.UpdateDescriptionAsync(name, description, ct);
        if (!updated)
            return Result<RoleDto>.Failure($"Role '{name}' does not exist.");

        var counts = await roles.CountAssignmentsAsync(ct);
        return Result<RoleDto>.Success(new RoleDto(
            name, description, counts.TryGetValue(name, out var c) ? c : 0));
    }

    public async Task<Result<bool>> DeleteRoleAsync(string name, CancellationToken ct = default)
    {
        var nameError = ValidateName(name);
        if (nameError is not null) return Result<bool>.Failure(nameError);
        name = name.Trim();

        var deleted = await roles.DeleteAsync(name, ct);
        if (!deleted) return Result<bool>.Failure($"Role '{name}' does not exist.");

        // Remove from every employee that had it so dangling refs don't linger.
        await employees.RemoveRoleFromAllAsync(name, ct);
        return Result<bool>.Success(true);
    }

    // ── Assignment ──

    public async Task<EmployeeRolesPage> ListEmployeesAsync(
        string? search, int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var (total, items) = await employees.ListAsync(
            search, page, pageSize, position: null, ct);

        return new EmployeeRolesPage(
            Total: total,
            Page: page,
            PageSize: pageSize,
            Items: items
                .Select(e => new EmployeeWithRolesDto(
                    e.EmployeeId, e.DisplayName, e.UserName, e.Roles))
                .ToList());
    }

    public async Task<Result<EmployeeWithRolesDto>> SetEmployeeRolesAsync(
        string employeeId, IReadOnlyList<string> rolesToAssign, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<EmployeeWithRolesDto>.Failure("Employee ID is required.");

        // De-dupe + trim, drop empties.
        var cleaned = rolesToAssign
            .Select(r => r?.Trim() ?? string.Empty)
            .Where(r => r.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Every assigned role must exist in the catalog.
        if (cleaned.Count > 0)
        {
            var catalog = (await roles.ListAsync(ct))
                .Select(r => r.Name)
                .ToHashSet(StringComparer.Ordinal);
            var unknown = cleaned.Where(r => !catalog.Contains(r)).ToList();
            if (unknown.Count > 0)
                return Result<EmployeeWithRolesDto>.Failure(
                    $"Unknown role(s): {string.Join(", ", unknown)}");
        }

        var ok = await employees.SetRolesAsync(employeeId, cleaned, ct);
        if (!ok)
            return Result<EmployeeWithRolesDto>.Failure(
                $"Employee '{employeeId}' does not exist.");

        var fresh = await employees.GetByIdAsync(employeeId, ct);
        return fresh is null
            ? Result<EmployeeWithRolesDto>.Failure(
                $"Employee '{employeeId}' disappeared during update.")
            : Result<EmployeeWithRolesDto>.Success(new EmployeeWithRolesDto(
                fresh.Value.EmployeeId, fresh.Value.DisplayName,
                fresh.Value.UserName, fresh.Value.Roles));
    }

    // ── Validation helpers ──

    private static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Role name is required.";
        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
            return $"Role name must be {MaxNameLength} characters or fewer.";
        // Comma confuses Postgres text[] literal parsing — keep it clean.
        if (trimmed.Contains(','))
            return "Role name may not contain a comma.";
        return null;
    }

    private static string? TrimDescription(string? d) =>
        string.IsNullOrWhiteSpace(d) ? null : d.Trim();

    private static string? ValidateDescription(string? description) =>
        description is { Length: > MaxDescriptionLength }
            ? $"Description must be {MaxDescriptionLength} characters or fewer."
            : null;
}
