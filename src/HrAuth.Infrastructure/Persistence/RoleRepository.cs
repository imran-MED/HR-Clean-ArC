using HrAuth.Application.Persistence;
using HrAuth.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrAuth.Infrastructure.Persistence;

/// <summary><see cref="IRoleRepository"/> backed by EF Core / PostgreSQL.</summary>
public sealed class RoleRepository(HrAuthDbContext db) : IRoleRepository
{
    public async Task<IReadOnlyList<(string Name, string? Description)>> ListAsync(
        CancellationToken ct = default)
    {
        // Project to an anonymous type server-side. Npgsql can't materialize a
        // PostgreSQL composite `record` back into a .NET ValueTuple, so we
        // convert to the tuple in memory after the round-trip.
        var rows = await db.Roles
            .OrderBy(r => r.Name)
            .Select(r => new { r.Name, r.Description })
            .ToListAsync(ct);
        return rows.Select(r => (r.Name, r.Description)).ToList();
    }

    public Task<bool> ExistsAsync(string name, CancellationToken ct = default)
        => db.Roles.AnyAsync(r => r.Name == name, ct);

    public async Task<bool> AddAsync(
        string name, string? description, CancellationToken ct = default)
    {
        if (await db.Roles.AnyAsync(r => r.Name == name, ct)) return false;
        db.Roles.Add(new RoleEntity
        {
            Name = name,
            Description = description,
            CreatedUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateDescriptionAsync(
        string name, string? description, CancellationToken ct = default)
    {
        var existing = await db.Roles.FindAsync([name], ct);
        if (existing is null) return false;
        existing.Description = description;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(string name, CancellationToken ct = default)
    {
        var existing = await db.Roles.FindAsync([name], ct);
        if (existing is null) return false;
        db.Roles.Remove(existing);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyDictionary<string, int>> CountAssignmentsAsync(
        CancellationToken ct = default)
    {
        // Unnest the employees.Roles array and group on the server. We use a
        // FromSqlRaw because EF translation of unnest() isn't supported.
        // Read all (small directory) and aggregate in memory — simpler and fine
        // for our scale (~1k employees, a few roles each).
        var pairs = await db.Employees
            .Select(e => new { e.EmployeeId, e.Roles })
            .ToListAsync(ct);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var p in pairs)
        {
            foreach (var role in p.Roles)
            {
                counts.TryGetValue(role, out var n);
                counts[role] = n + 1;
            }
        }
        return counts;
    }
}
