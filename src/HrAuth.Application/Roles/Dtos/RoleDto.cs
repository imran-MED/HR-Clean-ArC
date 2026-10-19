namespace HrAuth.Application.Roles.Dtos;

/// <summary>One entry in the role catalog.</summary>
public sealed record RoleDto(string Name, string? Description, int AssignedTo);

/// <summary>Payload for POST/PUT /api/roles.</summary>
public sealed record RoleUpsertRequest(string Name, string? Description);

/// <summary>One employee row on the assignment tab.</summary>
public sealed record EmployeeWithRolesDto(
    string EmployeeId,
    string? DisplayName,
    string? UserName,
    IReadOnlyList<string> Roles);

/// <summary>Paginated employee result used by the Roles assignment tab.</summary>
public sealed record EmployeeRolesPage(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<EmployeeWithRolesDto> Items);

/// <summary>Payload for PUT /api/employees/{id}/roles.</summary>
public sealed record AssignRolesRequest(IReadOnlyList<string> Roles);
