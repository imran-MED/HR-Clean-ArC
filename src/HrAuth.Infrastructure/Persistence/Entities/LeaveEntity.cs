namespace HrAuth.Infrastructure.Persistence.Entities;

/// <summary>A stored employee leave fetched from SmartHCM.</summary>
public sealed class LeaveEntity
{
    public long Id { get; set; }

    public required string EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? LeaveType { get; set; }
    public string? LeaveName { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string? Status { get; set; }
    public string? StatusId { get; set; }
    public double? DayHourRequested { get; set; }
    public string? DayType { get; set; }
    public DateTime FetchedUtc { get; set; }
}
