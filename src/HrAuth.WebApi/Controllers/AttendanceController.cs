using HrAuth.Application.Attendance;
using HrAuth.Application.Attendance.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace HrAuth.WebApi.Controllers;

/// <summary>
/// Attendance machine data endpoints.
/// </summary>
[ApiController]
[Route("api/attendance")]
[Produces("application/json")]
public sealed class AttendanceController(
    IAttendanceReconciliationService reconciler,
    IRawPunchProvider rawPunchProvider,
    IAttendanceSummaryProvider summaryProvider) : ControllerBase
{
    /// <summary>
    /// Upload an attendance machine export file. Each line is
    /// <c>EmployeeId Date Time Status Verify WorkCode Reserved</c>.
    /// Employee IDs are normalized to 8 digits, then each employee/day is
    /// reconciled against their roster to flag scheduled periods with no
    /// matching punch.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    [ProducesResponseType(typeof(AttendanceReconciliationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(
        IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file was uploaded." });

        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync(ct);

        var result = await reconciler.ReconcileAsync(file.FileName, content, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// Re-reconcile an edited punch set. The Upload screen sends the corrected
    /// punches back as machine-export text (employee IDs already 8 digits) and
    /// receives a fresh reconciliation — used after the operator fixes wrong
    /// check-in/check-out entries, before downloading the corrected file.
    /// </summary>
    [HttpPost("reconcile")]
    [ProducesResponseType(typeof(AttendanceReconciliationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reconcile(
        [FromBody] ReconcileTextRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = "No punches were provided." });

        var result = await reconciler.ReconcileAsync(
            string.IsNullOrWhiteSpace(request.FileName) ? "corrected.txt" : request.FileName,
            request.Content, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Body for <see cref="Reconcile"/>: a file name and the punch text.</summary>
    public sealed record ReconcileTextRequest(string? FileName, string Content);

    /// <summary>
    /// Source C — pull raw punches straight from SmartHCM's
    /// <c>TOM_MACHINE_DATA</c> (direct Oracle) for a date range and reconcile
    /// them exactly like an uploaded file. <paramref name="employeeId"/> is
    /// optional; omit for all employees. Dates are <c>yyyy-MM-dd</c>.
    /// </summary>
    [HttpGet("fetch-raw")]
    [ProducesResponseType(typeof(AttendanceReconciliationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> FetchRaw(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] string? employeeId, CancellationToken ct)
    {
        if (to < from)
            return BadRequest(new { error = "'to' must not be before 'from'." });

        var punches = await rawPunchProvider.GetPunchFileAsync(from, to, employeeId, ct);
        if (!punches.IsSuccess || punches.Value is null)
            return BadRequest(new { error = punches.Error });

        if (punches.Value.Length == 0)
            return BadRequest(new { error = "No punches were found for the given range." });

        var result = await reconciler.ReconcileAsync(
            $"smarthcm-oracle_{from:yyyyMMdd}-{to:yyyyMMdd}.txt", punches.Value, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// Source B — fetch SmartHCM's computed daily attendance summary for one
    /// employee via <c>Attendance_GetAttendance</c> (the Dawam mobile app's
    /// read path). Requires that employee's own <paramref name="password"/>
    /// (plaintext; hashed before transmission). Dates are <c>yyyy-MM-dd</c>.
    /// </summary>
    [HttpGet("fetch")]
    [ProducesResponseType(typeof(IReadOnlyList<AttendanceDaySummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Fetch(
        [FromQuery] string employeeId, [FromQuery] string password,
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            return BadRequest(new { error = "employeeId is required." });
        if (to < from)
            return BadRequest(new { error = "'to' must not be before 'from'." });

        var result = await summaryProvider.GetAsync(employeeId, password ?? string.Empty, from, to, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }
}
