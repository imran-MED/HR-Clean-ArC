namespace HrAuth.Application.Persistence;

/// <summary>
/// Persistence port for the role catalog (table <c>roles</c>). Roles are
/// referenced by name; per-employee assignments live in
/// <c>employees.Roles text[]</c>.
/// </summary>
public interface IRoleRepository
{
    /// <summary>Return the catalog ordered by name.</summary>
    Task<IReadOnlyList<(string Name, string? Description)>> ListAsync(
        CancellationToken ct = default);

    /// <summary>True if a role with this exact name exists.</summary>
    Task<bool> ExistsAsync(string name, CancellationToken ct = default);

    /// <summary>Insert a new role. Returns false if the name is taken.</summary>
    Task<bool> AddAsync(string name, string? description, CancellationToken ct = default);

    /// <summary>
    /// Update the description on an existing role. Returns false if the role
    /// is not in the catalog.
    /// </summary>
    Task<bool> UpdateDescriptionAsync(
        string name, string? description, CancellationToken ct = default);

    /// <summary>Delete a role from the catalog.</summary>
    Task<bool> DeleteAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Count how many employees have each role. Used to populate the
    /// <c>AssignedTo</c> column in the catalog view.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> CountAssignmentsAsync(
        CancellationToken ct = default);
}
