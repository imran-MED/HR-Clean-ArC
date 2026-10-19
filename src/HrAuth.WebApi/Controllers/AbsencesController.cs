using HrAuth.Application.Absences;
using HrAuth.Application.Absences.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace HrAuth.WebApi.Controllers;

/// <summary>
/// Teaching-staff absences behind the monthly D33 deduction, and the
/// reference data for the absence-rate page. Both come from the HR API; the
/// caller's bearer token is forwarded.
/// </summary>
[ApiController]
[Produces("application/json")]
public sealed class AbsencesController(IAbsenceProvider absences) : ControllerBase
{
    /// <summary>
    /// A page of absent blocks with each block's deduction amount and the
    /// grand total of the whole query. Give <c>payMonth=2026-09</c> for the
    /// payroll period 16-Aug..15-Sep, or an explicit <c>from</c>/<c>to</c>.
    /// </summary>
    [HttpGet("api/absences")]
    [ProducesResponseType(typeof(AbsencePageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] string? payMonth,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? gender,
        [FromQuery] string? employeeId,
        [FromQuery] bool includeSynthesized = true,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var token = BearerToken();
        if (token is null)
            return Unauthorized(new { error = "Sign in first — no bearer token supplied." });

        var result = await absences.GetPageAsync(new AbsenceQuery
        {
            PayMonth = payMonth,
            From = from,
            To = to,
            Gender = gender,
            EmployeeId = employeeId,
            IncludeSynthesized = includeSynthesized,
            PageNumber = pageNumber,
            PageSize = pageSize,
        }, token, ct);

        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>All SmartHCM grades, or only those <c>positionId</c> allows.</summary>
    [HttpGet("api/absence-rates/grades")]
    [ProducesResponseType(typeof(IReadOnlyList<GradeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Grades([FromQuery] string? positionId, CancellationToken ct)
    {
        var token = BearerToken();
        if (token is null)
            return Unauthorized(new { error = "Sign in first — no bearer token supplied." });

        var result = await absences.GetGradesAsync(positionId, token, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>All SmartHCM positions with their head-count and allowed grades.</summary>
    [HttpGet("api/absence-rates/positions")]
    [ProducesResponseType(typeof(IReadOnlyList<PositionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Positions([FromQuery] bool includeInactive, CancellationToken ct)
    {
        var token = BearerToken();
        if (token is null)
            return Unauthorized(new { error = "Sign in first — no bearer token supplied." });

        var result = await absences.GetPositionsAsync(includeInactive, token, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Every attendance period, marked with whether it is part of the absence report.</summary>
    [HttpGet("api/absence-rates/periods")]
    [ProducesResponseType(typeof(IReadOnlyList<AttendancePeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Periods(CancellationToken ct)
    {
        var token = BearerToken();
        if (token is null)
            return Unauthorized(new { error = "Sign in first — no bearer token supplied." });

        var result = await absences.GetPeriodsAsync(token, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// The position × period monthly amounts in force for <c>payMonth</c>
    /// (yyyy-MM; defaults to the current pay month).
    /// </summary>
    [HttpGet("api/absence-rates")]
    [ProducesResponseType(typeof(AbsenceRateMatrixDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Rates([FromQuery] string? payMonth, CancellationToken ct)
    {
        var token = BearerToken();
        if (token is null)
            return Unauthorized(new { error = "Sign in first — no bearer token supplied." });

        var result = await absences.GetRatesAsync(payMonth, token, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// Saves changed monthly amounts from <c>effectiveFrom</c> onward. The HR
    /// API allows HR admins only and refuses months before the current one.
    /// </summary>
    [HttpPut("api/absence-rates")]
    [ProducesResponseType(typeof(AbsenceRateMatrixDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveRates(
        [FromBody] SaveAbsenceRatesRequest request, CancellationToken ct)
    {
        var token = BearerToken();
        if (token is null)
            return Unauthorized(new { error = "Sign in first — no bearer token supplied." });

        var result = await absences.SaveRatesAsync(request, token, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error, errors = result.Errors });
    }

    /// <summary>Every saved rate version, newest first.</summary>
    [HttpGet("api/absence-rates/history")]
    [ProducesResponseType(typeof(IReadOnlyList<AbsenceRateCellDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RateHistory(
        [FromQuery] int? positionId, [FromQuery] int? periodId, CancellationToken ct)
    {
        var token = BearerToken();
        if (token is null)
            return Unauthorized(new { error = "Sign in first — no bearer token supplied." });

        var result = await absences.GetRateHistoryAsync(positionId, periodId, token, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Whether the signed-in user may edit the rates (HR admin).</summary>
    [HttpGet("api/absence-rates/rights")]
    [ProducesResponseType(typeof(AbsenceRateRightsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RateRights(CancellationToken ct)
    {
        var token = BearerToken();
        if (token is null)
            return Unauthorized(new { error = "Sign in first — no bearer token supplied." });

        var result = await absences.GetRateRightsAsync(token, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// Late / early blocks beyond the grace minutes, with totals across every
    /// page. <c>payMonth=2026-09</c> = 16-Aug..15-Sep, or give <c>from</c>/<c>to</c>.
    /// </summary>
    [HttpGet("api/late-early")]
    [ProducesResponseType(typeof(LateEarlyPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LateEarly(
        [FromQuery] string? payMonth,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? gender,
        [FromQuery] string? employeeId,
        [FromQuery] bool includeLeaveCancelled = true,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var token = BearerToken();
        if (token is null)
            return Unauthorized(new { error = "Sign in first — no bearer token supplied." });

        var result = await absences.GetLateEarlyPageAsync(new LateEarlyQuery
        {
            PayMonth = payMonth,
            From = from,
            To = to,
            Gender = gender,
            EmployeeId = employeeId,
            IncludeLeaveCancelled = includeLeaveCancelled,
            PageNumber = pageNumber,
            PageSize = pageSize,
        }, token, ct);

        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Every attendance rule for <c>payMonth</c> (yyyy-MM; default current).</summary>
    [HttpGet("api/attendance-rules")]
    [ProducesResponseType(typeof(AttendanceRulesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Rules([FromQuery] string? payMonth, CancellationToken ct)
    {
        var token = BearerToken();
        if (token is null)
            return Unauthorized(new { error = "Sign in first — no bearer token supplied." });

        var result = await absences.GetRulesAsync(payMonth, token, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Saves changed rules from <c>effectiveFrom</c> onward (HR admins only).</summary>
    [HttpPut("api/attendance-rules")]
    [ProducesResponseType(typeof(AttendanceRulesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveRules([FromBody] SaveAttendanceRulesRequest request, CancellationToken ct)
    {
        var token = BearerToken();
        if (token is null)
            return Unauthorized(new { error = "Sign in first — no bearer token supplied." });

        var result = await absences.SaveRulesAsync(request, token, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error, errors = result.Errors });
    }

    /// <summary>Every saved rule version, newest first.</summary>
    [HttpGet("api/attendance-rules/history")]
    [ProducesResponseType(typeof(IReadOnlyList<AttendanceRuleVersionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RuleHistory([FromQuery] string? key, CancellationToken ct)
    {
        var token = BearerToken();
        if (token is null)
            return Unauthorized(new { error = "Sign in first — no bearer token supplied." });

        var result = await absences.GetRuleHistoryAsync(key, token, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Whether the signed-in user may edit the rules (HR admin).</summary>
    [HttpGet("api/attendance-rules/rights")]
    [ProducesResponseType(typeof(AbsenceRateRightsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RuleRights(CancellationToken ct)
    {
        var token = BearerToken();
        if (token is null)
            return Unauthorized(new { error = "Sign in first — no bearer token supplied." });

        var result = await absences.GetRuleRightsAsync(token, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    private string? BearerToken()
    {
        var header = Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : null;
    }
}
