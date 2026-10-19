using HrAuth.Application.Attendance;
using HrAuth.Application.Reports;
using HrAuth.Application.Reports.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace HrAuth.WebApi.Controllers;

/// <summary>
/// Attendance reports and adjustment actions taken from them.
/// </summary>
[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public sealed class ReportsController(
    IDailyAbsentReportProvider absentReport,
    IMissingPunchReportProvider missingPunchReport,
    IAttendanceAdjustmentProvider adjustments) : ControllerBase
{
    /// <summary>
    /// Missing-punches detail report: shift halves with exactly one punch,
    /// which cannot be paired into a check-in + check-out. Defaults to today
    /// when no range is given.
    /// </summary>
    [HttpGet("missing-punches")]
    [ProducesResponseType(typeof(IReadOnlyList<MissingPunchRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MissingPunches(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] string? employeeId, CancellationToken ct)
    {
        var fromDate = from ?? DateOnly.FromDateTime(DateTime.Today);
        var toDate = to ?? fromDate;
        if (toDate < fromDate)
            return BadRequest(new { error = "'to' must not be before 'from'." });

        var result = await missingPunchReport.GetAsync(
            fromDate, toDate, employeeId, BearerToken(), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// Daily absent report (teaching staff / admin). One row per
    /// employee / day / period-group whose attendance status is neither
    /// present nor on leave. Defaults to today when no range is given.
    /// </summary>
    [HttpGet("daily-absent")]
    [ProducesResponseType(typeof(IReadOnlyList<DailyAbsentRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DailyAbsent(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] string? employeeId, CancellationToken ct)
    {
        var fromDate = from ?? DateOnly.FromDateTime(DateTime.Today);
        var toDate = to ?? fromDate;
        if (toDate < fromDate)
            return BadRequest(new { error = "'to' must not be before 'from'." });

        var result = await absentReport.GetAsync(
            fromDate, toDate, employeeId, BearerToken(), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// Record an attendance adjustment for an absent employee/period. The
    /// signed-in user's bearer token is forwarded to the HR API, which
    /// enforces that user's adjust-attendance rights.
    /// </summary>
    [HttpPost("daily-absent/adjust")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Adjust(
        [FromBody] AdjustRequest request, CancellationToken ct)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.EmployeeId)
            || request.AttendanceDate == default)
        {
            return BadRequest(new { error = "employeeId and attendanceDate are required." });
        }

        var token = BearerToken();
        if (token is null)
            return Unauthorized(new { error = "Sign in first — no bearer token supplied." });

        var result = await adjustments.AddAsync(
            request.EmployeeId, request.AttendanceDate, request.Notes, token, ct);
        return result.IsSuccess
            ? Ok(new { message = "Adjustment recorded.", response = result.Value })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Remove a previously recorded attendance adjustment.</summary>
    [HttpDelete("daily-absent/adjust/{attendanceId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteAdjustment(
        string attendanceId, CancellationToken ct)
    {
        var token = BearerToken();
        if (token is null)
            return Unauthorized(new { error = "Sign in first — no bearer token supplied." });

        var result = await adjustments.DeleteAsync(attendanceId, token, ct);
        return result.IsSuccess
            ? Ok(new { message = "Adjustment removed.", response = result.Value })
            : BadRequest(new { error = result.Error });
    }

    private string? BearerToken()
    {
        var header = Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : null;
    }

    public sealed record AdjustRequest(
        string EmployeeId, DateTime AttendanceDate, string? Notes);
}
