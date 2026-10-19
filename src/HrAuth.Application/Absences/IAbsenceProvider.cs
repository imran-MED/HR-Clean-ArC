using HrAuth.Application.Absences.Dtos;
using HrAuth.Application.Common;

namespace HrAuth.Application.Absences;

/// <summary>
/// Port for the teaching-staff absences behind the D33 deduction and the
/// reference data for configuring absence rates. Implemented in
/// Infrastructure over the HR API; <c>bearerToken</c> is the signed-in user's
/// token, forwarded so the HR API enforces that user's access.
/// </summary>
public interface IAbsenceProvider
{
    /// <summary>A page of absent blocks with the grand total of the whole query.</summary>
    Task<Result<AbsencePageDto>> GetPageAsync(
        AbsenceQuery query, string? bearerToken, CancellationToken ct = default);

    /// <summary>All SmartHCM grades, or only those <paramref name="positionId"/> allows.</summary>
    Task<Result<IReadOnlyList<GradeDto>>> GetGradesAsync(
        string? positionId, string? bearerToken, CancellationToken ct = default);

    /// <summary>All SmartHCM positions with their allowed grades.</summary>
    Task<Result<IReadOnlyList<PositionDto>>> GetPositionsAsync(
        bool includeInactive, string? bearerToken, CancellationToken ct = default);

    /// <summary>Every attendance period, marked with whether it is part of the absence report.</summary>
    Task<Result<IReadOnlyList<AttendancePeriodDto>>> GetPeriodsAsync(
        string? bearerToken, CancellationToken ct = default);

    /// <summary>The position × period rates in force for <paramref name="payMonth"/> (yyyy-MM; null = current).</summary>
    Task<Result<AbsenceRateMatrixDto>> GetRatesAsync(
        string? payMonth, string? bearerToken, CancellationToken ct = default);

    /// <summary>Saves changed rates; the HR API allows HR admins only.</summary>
    Task<Result<AbsenceRateMatrixDto>> SaveRatesAsync(
        SaveAbsenceRatesRequest request, string? bearerToken, CancellationToken ct = default);

    /// <summary>Every saved version, newest first, optionally for one position and/or period.</summary>
    Task<Result<IReadOnlyList<AbsenceRateCellDto>>> GetRateHistoryAsync(
        int? positionId, int? periodId, string? bearerToken, CancellationToken ct = default);

    /// <summary>Whether the signed-in user may edit the rates.</summary>
    Task<Result<AbsenceRateRightsDto>> GetRateRightsAsync(
        string? bearerToken, CancellationToken ct = default);

    /// <summary>A page of late / early blocks with totals across every page.</summary>
    Task<Result<LateEarlyPageDto>> GetLateEarlyPageAsync(
        LateEarlyQuery query, string? bearerToken, CancellationToken ct = default);

    /// <summary>Every attendance rule for <paramref name="payMonth"/> (yyyy-MM; null = current).</summary>
    Task<Result<AttendanceRulesDto>> GetRulesAsync(
        string? payMonth, string? bearerToken, CancellationToken ct = default);

    /// <summary>Saves changed rules; the HR API allows HR admins only.</summary>
    Task<Result<AttendanceRulesDto>> SaveRulesAsync(
        SaveAttendanceRulesRequest request, string? bearerToken, CancellationToken ct = default);

    /// <summary>Every saved rule version, newest first, optionally for one rule.</summary>
    Task<Result<IReadOnlyList<AttendanceRuleVersionDto>>> GetRuleHistoryAsync(
        string? key, string? bearerToken, CancellationToken ct = default);

    /// <summary>Whether the signed-in user may edit the rules.</summary>
    Task<Result<AbsenceRateRightsDto>> GetRuleRightsAsync(
        string? bearerToken, CancellationToken ct = default);
}
