namespace HrAuth.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core persistence model for an imported attendance punch
/// (table "attendance_records").
/// </summary>
public sealed class AttendanceRecordEntity
{
    public long Id { get; set; }

    /// <summary>Employee ID, normalized to 8 digits.</summary>
    public string EmployeeId { get; set; } = string.Empty;

    /// <summary>Employee ID exactly as it appeared in the file.</summary>
    public string RawEmployeeId { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    public int Status { get; set; }
    public int Verify { get; set; }
    public int WorkCode { get; set; }
    public int Reserved { get; set; }

    /// <summary>Name of the uploaded file this punch came from.</summary>
    public string SourceFile { get; set; } = string.Empty;

    public DateTime ImportedUtc { get; set; }
}
