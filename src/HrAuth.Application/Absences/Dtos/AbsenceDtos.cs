namespace HrAuth.Application.Absences.Dtos;

/// <summary>
/// One absent block from the HR API's <c>GET /api/hr/absences</c>: an
/// employee / day / period group (GRP_MORNING = P1–P3, GRP_EVENING = P4–P6,
/// or P_&lt;id&gt;) with no punch anywhere in the block, and the D33
/// deduction amount it carries.
/// </summary>
public sealed record AbsenceRecordDto
{
    public required string AbsenceId { get; init; }        // {employeeId}-{yyyy-MM-dd}-{groupKey}
    public required string EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public string? Position { get; init; }
    public string? Department { get; init; }
    public required string AttendDate { get; init; }       // yyyy-MM-dd
    public string? Day { get; init; }                      // SUN, MON, …
    public required string GroupKey { get; init; }
    public string? Periods { get; init; }                  // e.g. "P1+P2+P3"
    public int PeriodCount { get; init; }
    public decimal RequiredHours { get; init; }
    public decimal PresentHours { get; init; }
    public decimal LeaveHours { get; init; }
    public decimal AbsentHours { get; init; }
    public decimal DeductionAmount { get; init; }
    public string? Flag { get; init; }                     // null / NO_BREAKUP_ROW / SYNTHESIZED_RECALCULATE
    public string? PatternFrom { get; init; }              // synthesized rows: the day whose pattern was copied
}

/// <summary>A page of absences plus the total of the whole query.</summary>
public sealed record AbsencePageDto
{
    public required IReadOnlyList<AbsenceRecordDto> Items { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasPrevious { get; init; }
    public bool HasNext { get; init; }

    /// <summary>Σ deduction amount across every page, not just this one.</summary>
    public decimal GrandTotalAmount { get; init; }

    /// <summary>The inclusive range the HR API resolved (yyyy-MM-dd).</summary>
    public string? From { get; init; }
    public string? To { get; init; }
}

/// <summary>Filters for <see cref="IAbsenceProvider.GetPageAsync"/>.</summary>
public sealed record AbsenceQuery
{
    /// <summary>"yyyy-MM": the payroll period 16th of the previous month → 15th.</summary>
    public string? PayMonth { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public string? Gender { get; init; }                   // M / F
    public string? EmployeeId { get; init; }
    public bool IncludeSynthesized { get; init; } = true;
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

/// <summary>A SmartHCM grade (HRS_GRADE).</summary>
public sealed record GradeDto
{
    public required string Grade { get; init; }
    public string? ShortName { get; init; }
    public string? Description { get; init; }
    public string? LevelNo { get; init; }
    public string? TypeId { get; init; }
    public int EmployeeCount { get; init; }
}

/// <summary>A SmartHCM position (HRS_POSITION) with the grades it allows.</summary>
public sealed record PositionDto
{
    public required string PositionId { get; init; }
    public string? Title { get; init; }
    public bool Active { get; init; }
    public int EmployeeCount { get; init; }
    public IReadOnlyList<string> Grades { get; init; } = [];
}

/// <summary>An attendance period; <see cref="Included"/> = part of the absence report.</summary>
public sealed record AttendancePeriodDto
{
    public int PeriodId { get; init; }
    public string? ShortName { get; init; }
    public string? Description { get; init; }
    public decimal RequiredHours { get; init; }
    public int? OrderNo { get; init; }
    public bool Included { get; init; }
}

/// <summary>The position × period monthly amounts in force for one pay month.</summary>
public sealed record AbsenceRateMatrixDto
{
    public required string PayMonth { get; init; }             // yyyy-MM
    public required string EarliestEditableMonth { get; init; } // yyyy-MM
    public int DaysPerMonth { get; init; }
    public IReadOnlyList<int> PositionIds { get; init; } = [];
    public IReadOnlyList<int> PeriodIds { get; init; } = [];
    public IReadOnlyList<AbsenceRateCellDto> Cells { get; init; } = [];
}

/// <summary>One version of a position / period rate.</summary>
public sealed record AbsenceRateCellDto
{
    public int PositionId { get; init; }
    public int PeriodId { get; init; }
    public decimal? MonthlyAmount { get; init; }               // null = withdrawn
    public decimal? DailyRate { get; init; }
    public required string EffectiveFrom { get; init; }        // yyyy-MM
    public string? Remarks { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>Changed rates to save from <see cref="EffectiveFrom"/> onward.</summary>
public sealed record SaveAbsenceRatesRequest
{
    public required string EffectiveFrom { get; init; }        // yyyy-MM
    public string? Remarks { get; init; }
    public IReadOnlyList<SaveAbsenceRateCell> Cells { get; init; } = [];
}

/// <summary>One changed cell; a null amount withdraws the rate.</summary>
public sealed record SaveAbsenceRateCell
{
    public int PositionId { get; init; }
    public int PeriodId { get; init; }
    public decimal? MonthlyAmount { get; init; }
}

/// <summary>Whether the signed-in user may edit the rates (HR admin).</summary>
public sealed record AbsenceRateRightsDto
{
    public bool CanEdit { get; init; }
}
