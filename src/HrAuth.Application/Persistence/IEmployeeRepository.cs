namespace HrAuth.Application.Persistence;

/// <summary>
/// Persistence port for employee identity records. Implemented in the
/// Infrastructure layer (Entity Framework Core / PostgreSQL).
/// </summary>
public interface IEmployeeRepository
{
    /// <summary>Insert or update an employee seen at login.</summary>
    Task UpsertAsync(
        string employeeId,
        string userName,
        string? displayName,
        IReadOnlyList<string> roles,
        CancellationToken ct = default);

    /// <summary>
    /// Look up display names for a set of employee IDs. Returns a map of
    /// employee ID → name; IDs with no stored name are simply omitted.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetNamesAsync(
        IReadOnlyCollection<string> employeeIds, CancellationToken ct = default);

    /// <summary>
    /// Look up positions for a set of employee IDs. Returns a map of employee
    /// ID → position; IDs with no stored position are simply omitted.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetPositionsAsync(
        IReadOnlyCollection<string> employeeIds, CancellationToken ct = default);

    /// <summary>
    /// Bulk-upsert employee display names from a directory CSV. Inserts a
    /// minimal row for IDs not seen before; updates only the display name
    /// for existing rows (preserves UserName/Roles/LastLoginUtc).
    /// Returns the number of rows actually written (inserted + updated).
    /// </summary>
    Task<int> BulkUpsertNamesAsync(
        IReadOnlyCollection<(string EmployeeId, string DisplayName)> rows,
        CancellationToken ct = default);

    /// <summary>
    /// Bulk-upsert directory rows carrying name plus organizational profile
    /// (position / supervisor). Inserts a minimal row for unseen IDs; for
    /// existing rows updates DisplayName, and updates Position /
    /// SupervisorEmployeeId only when the supplied value is non-null (so a
    /// blank column never erases an existing value). Login metadata
    /// (UserName / Roles / LastLoginUtc) is preserved. Returns the number of
    /// rows written (inserted + updated).
    /// </summary>
    Task<int> BulkUpsertDirectoryAsync(
        IReadOnlyCollection<(string EmployeeId, string DisplayName,
                             string? Position, string? SupervisorEmployeeId)> rows,
        CancellationToken ct = default);

    /// <summary>Total employees currently in the directory.</summary>
    Task<int> CountAsync(CancellationToken ct = default);

    /// <summary>
    /// Paginated employee list with current roles. <paramref name="search"/>
    /// is matched against EmployeeId, UserName and DisplayName (case-insensitive
    /// contains). Returns (total, page, items).
    /// </summary>
    Task<(int Total,
        IReadOnlyList<(string EmployeeId, string? DisplayName, string? UserName,
                       IReadOnlyList<string> Roles, string? Position,
                       string? SupervisorEmployeeId)> Items)> ListAsync(
        string? search, int page, int pageSize, string? position,
        CancellationToken ct = default);

    /// <summary>
    /// Distinct, non-empty employee positions, sorted — powers the directory
    /// position filter.
    /// </summary>
    Task<IReadOnlyList<string>> ListPositionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Look up a single employee by ID. Returns null if not present.
    /// </summary>
    Task<(string EmployeeId, string? DisplayName, string? UserName,
          IReadOnlyList<string> Roles, string? Position,
          string? SupervisorEmployeeId)?> GetByIdAsync(
        string employeeId, CancellationToken ct = default);

    /// <summary>
    /// Replace the role list for one employee. Returns false if the employee
    /// does not exist.
    /// </summary>
    Task<bool> SetRolesAsync(
        string employeeId, IReadOnlyList<string> roles, CancellationToken ct = default);

    /// <summary>
    /// Remove a role name from every employee. Used when a role is deleted
    /// from the catalog so dangling references don't linger.
    /// </summary>
    Task<int> RemoveRoleFromAllAsync(string roleName, CancellationToken ct = default);

    /// <summary>
    /// Insert a single employee (manual add from the Directory tab). Returns
    /// false if the ID is already present.
    /// </summary>
    Task<bool> AddAsync(
        string employeeId,
        string displayName,
        string? userName,
        string? position,
        string? supervisorEmployeeId,
        CancellationToken ct = default);

    /// <summary>
    /// Update an employee's organizational profile (position / supervisor).
    /// Only non-null arguments are written, so a field the source did not
    /// report never erases an existing value. Returns false if the employee
    /// does not exist.
    /// </summary>
    Task<bool> UpdateProfileAsync(
        string employeeId,
        string? position,
        string? supervisorEmployeeId,
        CancellationToken ct = default);
}
