namespace HrAuth.Infrastructure.Persistence.Entities;

/// <summary>
/// A cached SmartHCM roster for one employee/day, refreshed at most once per
/// calendar day so roster updates are still picked up daily while repeat
/// reconciles (even across restarts) avoid re-hitting SmartHCM.
/// </summary>
public sealed class RosterCacheEntity
{
    public required string EmployeeId { get; set; }

    /// <summary>The attendance date the roster is for (yyyy-MM-dd).</summary>
    public required string AttendanceDate { get; set; }

    /// <summary>The roster serialized as JSON.</summary>
    public required string RosterJson { get; set; }

    /// <summary>When this entry was last fetched from SmartHCM (UTC).</summary>
    public DateTime FetchedUtc { get; set; }
}
