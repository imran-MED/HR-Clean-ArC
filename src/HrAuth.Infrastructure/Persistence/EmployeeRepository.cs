using HrAuth.Application.Persistence;
using HrAuth.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrAuth.Infrastructure.Persistence;

/// <summary><see cref="IEmployeeRepository"/> backed by EF Core / PostgreSQL.</summary>
public sealed class EmployeeRepository(HrAuthDbContext db) : IEmployeeRepository
{
    public async Task UpsertAsync(
        string employeeId, string userName, string? displayName,
        IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var existing = await db.Employees.FindAsync([employeeId], ct);

        if (existing is null)
        {
            db.Employees.Add(new EmployeeEntity
            {
                EmployeeId = employeeId,
                UserName = userName,
                DisplayName = displayName,
                Roles = [.. roles],
                FirstSeenUtc = now,
                LastLoginUtc = now,
            });
        }
        else
        {
            existing.UserName = userName;
            if (!string.IsNullOrWhiteSpace(displayName))
                existing.DisplayName = displayName;
            if (roles.Count > 0)
                existing.Roles = [.. roles];
            existing.LastLoginUtc = now;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(
        IReadOnlyCollection<string> employeeIds, CancellationToken ct = default)
    {
        if (employeeIds.Count == 0)
            return new Dictionary<string, string>();

        return await db.Employees
            .Where(e => employeeIds.Contains(e.EmployeeId) && e.DisplayName != null)
            .ToDictionaryAsync(e => e.EmployeeId, e => e.DisplayName!, ct);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetPositionsAsync(
        IReadOnlyCollection<string> employeeIds, CancellationToken ct = default)
    {
        if (employeeIds.Count == 0)
            return new Dictionary<string, string>();

        return await db.Employees
            .Where(e => employeeIds.Contains(e.EmployeeId) && e.Position != null)
            .ToDictionaryAsync(e => e.EmployeeId, e => e.Position!, ct);
    }

    public async Task<int> BulkUpsertNamesAsync(
        IReadOnlyCollection<(string EmployeeId, string DisplayName)> rows,
        CancellationToken ct = default)
    {
        if (rows.Count == 0) return 0;

        var now = DateTime.UtcNow;
        var ids = rows.Select(r => r.EmployeeId).ToList();
        var existing = await db.Employees
            .Where(e => ids.Contains(e.EmployeeId))
            .ToDictionaryAsync(e => e.EmployeeId, ct);

        var written = 0;
        foreach (var (id, name) in rows)
        {
            if (existing.TryGetValue(id, out var emp))
            {
                // Only touch DisplayName; don't overwrite real login metadata.
                if (emp.DisplayName != name)
                {
                    emp.DisplayName = name;
                    written++;
                }
            }
            else
            {
                db.Employees.Add(new EmployeeEntity
                {
                    EmployeeId = id,
                    UserName = string.Empty,
                    DisplayName = name,
                    Roles = [],
                    FirstSeenUtc = now,
                    // Schema is non-null; for CSV imports we treat the import
                    // as the "last seen" event. A real login will overwrite it.
                    LastLoginUtc = now,
                });
                written++;
            }
        }

        await db.SaveChangesAsync(ct);
        return written;
    }

    public async Task<int> BulkUpsertDirectoryAsync(
        IReadOnlyCollection<(string EmployeeId, string DisplayName,
                             string? Position, string? SupervisorEmployeeId)> rows,
        CancellationToken ct = default)
    {
        if (rows.Count == 0) return 0;

        var now = DateTime.UtcNow;
        var ids = rows.Select(r => r.EmployeeId).ToList();
        var existing = await db.Employees
            .Where(e => ids.Contains(e.EmployeeId))
            .ToDictionaryAsync(e => e.EmployeeId, ct);

        var written = 0;
        foreach (var (id, name, position, supervisorId) in rows)
        {
            if (existing.TryGetValue(id, out var emp))
            {
                var changed = false;
                if (!string.IsNullOrWhiteSpace(name) && emp.DisplayName != name)
                { emp.DisplayName = name; changed = true; }
                // Only non-null profile fields are written, so a blank column
                // never erases an existing value.
                if (position is not null && emp.Position != position)
                { emp.Position = position; changed = true; }
                if (supervisorId is not null && emp.SupervisorEmployeeId != supervisorId)
                { emp.SupervisorEmployeeId = supervisorId; changed = true; }
                if (changed) written++;
            }
            else
            {
                db.Employees.Add(new EmployeeEntity
                {
                    EmployeeId = id,
                    UserName = string.Empty,
                    DisplayName = name,
                    Position = position,
                    SupervisorEmployeeId = supervisorId,
                    Roles = [],
                    FirstSeenUtc = now,
                    LastLoginUtc = now,
                });
                written++;
            }
        }

        await db.SaveChangesAsync(ct);
        return written;
    }

    public Task<int> CountAsync(CancellationToken ct = default)
        => db.Employees.CountAsync(ct);

    public async Task<IReadOnlyList<string>> ListPositionsAsync(
        CancellationToken ct = default)
        => await db.Employees
            .Where(e => e.Position != null && e.Position != "")
            .Select(e => e.Position!)
            .Distinct()
            .OrderBy(p => p)
            .ToListAsync(ct);

    public async Task<(int Total,
        IReadOnlyList<(string EmployeeId, string? DisplayName, string? UserName,
                       IReadOnlyList<string> Roles, string? Position,
                       string? SupervisorEmployeeId)> Items)> ListAsync(
        string? search, int page, int pageSize, string? position,
        CancellationToken ct = default)
    {
        var q = db.Employees.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            // EF.Functions.ILike is the PostgreSQL case-insensitive LIKE.
            var pattern = $"%{s}%";
            q = q.Where(e =>
                EF.Functions.ILike(e.EmployeeId, pattern) ||
                (e.DisplayName != null && EF.Functions.ILike(e.DisplayName, pattern)) ||
                (e.UserName != null && EF.Functions.ILike(e.UserName, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(position))
        {
            var p = position.Trim();
            q = q.Where(e => e.Position == p);
        }

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderBy(e => e.EmployeeId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.EmployeeId, e.DisplayName, e.UserName, e.Roles,
                e.Position, e.SupervisorEmployeeId
            })
            .ToListAsync(ct);

        var items = rows
            .Select(e => (
                e.EmployeeId,
                (string?)e.DisplayName,
                (string?)e.UserName,
                (IReadOnlyList<string>)e.Roles,
                (string?)e.Position,
                (string?)e.SupervisorEmployeeId))
            .ToList();

        return (total, items);
    }

    public async Task<(string EmployeeId, string? DisplayName, string? UserName,
                       IReadOnlyList<string> Roles, string? Position,
                       string? SupervisorEmployeeId)?> GetByIdAsync(
        string employeeId, CancellationToken ct = default)
    {
        var e = await db.Employees.FindAsync([employeeId], ct);
        return e is null
            ? null
            : (e.EmployeeId, e.DisplayName, e.UserName,
               (IReadOnlyList<string>)e.Roles, e.Position, e.SupervisorEmployeeId);
    }

    public async Task<bool> SetRolesAsync(
        string employeeId, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        var e = await db.Employees.FindAsync([employeeId], ct);
        if (e is null) return false;
        e.Roles = [.. roles];
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> RemoveRoleFromAllAsync(
        string roleName, CancellationToken ct = default)
    {
        // Small directory — just load the affected rows and edit in place.
        var affected = await db.Employees
            .Where(e => e.Roles.Contains(roleName))
            .ToListAsync(ct);
        foreach (var e in affected)
            e.Roles = e.Roles.Where(r => r != roleName).ToList();
        if (affected.Count > 0)
            await db.SaveChangesAsync(ct);
        return affected.Count;
    }

    public async Task<bool> AddAsync(
        string employeeId, string displayName, string? userName,
        string? position, string? supervisorEmployeeId,
        CancellationToken ct = default)
    {
        var exists = await db.Employees
            .AnyAsync(e => e.EmployeeId == employeeId, ct);
        if (exists) return false;

        var now = DateTime.UtcNow;
        db.Employees.Add(new EmployeeEntity
        {
            EmployeeId = employeeId,
            UserName = userName ?? string.Empty,
            DisplayName = displayName,
            Position = position,
            SupervisorEmployeeId = supervisorEmployeeId,
            Roles = [],
            FirstSeenUtc = now,
            LastLoginUtc = now,
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateProfileAsync(
        string employeeId, string? position, string? supervisorEmployeeId,
        CancellationToken ct = default)
    {
        var e = await db.Employees.FindAsync([employeeId], ct);
        if (e is null) return false;

        // Only overwrite a field the source actually reported, so a missing
        // SmartHCM value never wipes a manually-entered one.
        if (position is not null) e.Position = position;
        if (supervisorEmployeeId is not null) e.SupervisorEmployeeId = supervisorEmployeeId;

        await db.SaveChangesAsync(ct);
        return true;
    }
}
