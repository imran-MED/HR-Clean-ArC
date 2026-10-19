namespace HrAuth.Infrastructure.Persistence.Entities;

/// <summary>EF Core persistence model for an employee (table "employees").</summary>
public sealed class EmployeeEntity
{
    /// <summary>8-digit employee ID — primary key.</summary>
    public string EmployeeId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    /// <summary>Job title / position (free text).</summary>
    public string? Position { get; set; }

    /// <summary>
    /// 8-digit ID of this employee's supervisor (another row in this table).
    /// Soft reference — validated at the application layer rather than via a
    /// hard FK, so directory rows can be added in any order. Null = no
    /// supervisor on record.
    /// </summary>
    public string? SupervisorEmployeeId { get; set; }

    public List<string> Roles { get; set; } = [];

    public DateTime FirstSeenUtc { get; set; }

    public DateTime LastLoginUtc { get; set; }
}
