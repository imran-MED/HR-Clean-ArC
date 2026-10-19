namespace HrAuth.Application.Absences.Dtos;

/// <summary>Filters for <see cref="IAbsenceProvider.GetLateEarlyPageAsync"/>.</summary>
public sealed record LateEarlyQuery
{
    /// <summary>"yyyy-MM": the payroll period 16th of the previous month → 15th.</summary>
    public string? PayMonth { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public string? Gender { get; init; }
    public string? EmployeeId { get; init; }
    public bool IncludeLeaveCancelled { get; init; } = true;
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

/// <summary>One employee / day / block late or early beyond the grace minutes.</summary>
public sealed record LateEarlyRecordDto
{
    public required string RecordId { get; init; }
    public required string EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public string? Position { get; init; }
    public string? Department { get; init; }
    public required string AttendDate { get; init; }   // yyyy-MM-dd
    public string? Day { get; init; }
    public required string BlockKey { get; init; }     // GRP_MORNING / GRP_EVENING / P_n
    public string? Periods { get; init; }
    public string? BlockStart { get; init; }           // HH:MI
    public string? BlockEnd { get; init; }
    public string? CheckIn { get; init; }
    public string? CheckOut { get; init; }
    public decimal PermissionHours { get; init; }
    public decimal LateMinutes { get; init; }
    public decimal EarlyMinutes { get; init; }
    public decimal TotalMinutes { get; init; }
    public bool CancelledByLeave { get; init; }
    public decimal MinutesBeforeLeave { get; init; }
    public string? Flag { get; init; }
}

/// <summary>A page of late / early blocks plus totals across every page.</summary>
public sealed record LateEarlyPageDto
{
    public required IReadOnlyList<LateEarlyRecordDto> Items { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasPrevious { get; init; }
    public bool HasNext { get; init; }
    public decimal TotalLateMinutes { get; init; }
    public decimal TotalEarlyMinutes { get; init; }
    public decimal TotalMinutes { get; init; }
    public int CancelledByLeaveCount { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
}

/// <summary>Every attendance rule as it stands for one pay month.</summary>
public sealed record AttendanceRulesDto
{
    public required string PayMonth { get; init; }
    public required string EarliestEditableMonth { get; init; }
    public IReadOnlyList<AttendanceRuleDto> Rules { get; init; } = [];
}

/// <summary>One rule: meaning, value in force, built-in default.</summary>
public sealed record AttendanceRuleDto
{
    public required string Key { get; init; }
    public required string Group { get; init; }        // scope / groups / absence / late_early
    public required string Type { get; init; }         // int / int_list / bool / time
    public string? LabelAr { get; init; }
    public string? LabelEn { get; init; }
    public string? DescriptionAr { get; init; }
    public required string Value { get; init; }
    public required string DefaultValue { get; init; }
    public bool IsDefault { get; init; }
    public int? Min { get; init; }
    public int? Max { get; init; }
    public string? EffectiveFrom { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>One saved version of a rule (null value = back to default).</summary>
public sealed record AttendanceRuleVersionDto
{
    public required string Key { get; init; }
    public string? Value { get; init; }
    public required string EffectiveFrom { get; init; }
    public string? Remarks { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>Changed rules to save from <see cref="EffectiveFrom"/> onward.</summary>
public sealed record SaveAttendanceRulesRequest
{
    public required string EffectiveFrom { get; init; }
    public string? Remarks { get; init; }
    public IReadOnlyList<SaveAttendanceRule> Rules { get; init; } = [];
}

/// <summary>One changed rule; a null value returns it to the default.</summary>
public sealed record SaveAttendanceRule
{
    public required string Key { get; init; }
    public string? Value { get; init; }
}
