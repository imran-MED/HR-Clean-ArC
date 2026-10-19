namespace HrAuth.Application.Reports.Dtos;

/// <summary>
/// One row of the missing-punches detail report: an employee / day / shift-half
/// holding exactly one punch, which therefore cannot be paired into a
/// check-in + check-out. <see cref="MissingType"/> says which side is missing;
/// the lone punch appears in the opposite column.
/// </summary>
public sealed record MissingPunchRowDto
{
    public required string AttDate { get; init; }        // yyyy-MM-dd
    public required string EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public string? Position { get; init; }
    public string? Department { get; init; }
    public required string PeriodGroup { get; init; }    // MORNING 1,2,3 / EVENING 4,5,6 / P_n
    public string? Periods { get; init; }                 // e.g. "P1+P2"
    public string? CheckIn { get; init; }                 // "HH:mm" when the check-out is missing
    public string? CheckOut { get; init; }                // "HH:mm" when the check-in is missing
    public required string MissingType { get; init; }     // Missing Check-In / Missing Check-Out
}
