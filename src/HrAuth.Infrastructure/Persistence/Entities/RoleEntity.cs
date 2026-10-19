namespace HrAuth.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core model for the role catalog (table <c>roles</c>). Per-employee
/// role assignments live in <c>employees.Roles text[]</c>; this table just
/// defines the legal names.
/// </summary>
public sealed class RoleEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedUtc { get; set; }
}
