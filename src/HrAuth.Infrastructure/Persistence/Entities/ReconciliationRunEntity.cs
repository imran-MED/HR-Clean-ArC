namespace HrAuth.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core persistence model for one roster-reconciliation run
/// (table "reconciliation_runs"). The per-employee/day breakdown is stored
/// as a JSONB document.
/// </summary>
public sealed class ReconciliationRunEntity
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }

    public int TotalLines { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }

    public int TotalMissingPeriods { get; set; }
    public int EmployeesWithoutRoster { get; set; }
    public int TotalLateMinutes { get; set; }
    public int TotalEarlyDepartureMinutes { get; set; }

    /// <summary>Serialized employee/day breakdown (stored as JSONB).</summary>
    public string DaysJson { get; set; } = "[]";
}
