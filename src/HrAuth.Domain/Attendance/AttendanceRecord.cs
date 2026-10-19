namespace HrAuth.Domain.Attendance;

/// <summary>
/// A single punch read from an attendance machine export file. The line
/// layout is: <c>EmployeeId  Date  Time  Status  Verify  WorkCode  Reserved</c>.
/// </summary>
public sealed class AttendanceRecord
{
    /// <summary>1-based line number in the source file.</summary>
    public required int LineNumber { get; init; }

    /// <summary>Employee ID, normalized to 8 digits via <see cref="EmployeeIdRules"/>.</summary>
    public required string EmployeeId { get; init; }

    /// <summary>The employee ID exactly as it appeared in the file.</summary>
    public required string RawEmployeeId { get; init; }

    /// <summary>The punch date and time.</summary>
    public required DateTime Timestamp { get; init; }

    public int Status { get; init; }
    public int Verify { get; init; }
    public int WorkCode { get; init; }
    public int Reserved { get; init; }

    /// <summary>True when normalization changed the ID.</summary>
    public bool IdWasNormalized => EmployeeId != RawEmployeeId;
}
