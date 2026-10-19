using HrAuth.Application.Abstractions;
using HrAuth.Application.Leaves.Requests;
using HrAuth.Application.Leaves.Requests.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace HrAuth.WebApi.Controllers;

/// <summary>
/// Leave workflow — apply for leave, the approval queue, and approve / reject /
/// withdraw — over the same SmartHCM operations the Dawam app uses. Every
/// endpoint acts as the signed-in user: the bearer token identifies the
/// session, and the user's SmartHCM credential (captured at login, kept
/// server-side) is used for the SmartHCM calls. 401 means "sign in again".
/// </summary>
[ApiController]
[Route("api/leave-requests")]
[Produces("application/json")]
public sealed class LeaveRequestsController(
    ILeaveRequestService leaves,
    ISmartHcmCredentialStore credentials) : ControllerBase
{
    private const string SignInAgain =
        "Your session has no SmartHCM sign-in (or the server restarted). Please sign in again.";

    /// <summary>Leave types the signed-in employee may apply for.</summary>
    [HttpGet("types")]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Types(CancellationToken ct)
    {
        if (Caller() is not { } caller) return Unauthorized(new { error = SignInAgain });
        var r = await leaves.GetLeaveTypesAsync(caller, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Effective number of days for a range (weekends / holidays aware).</summary>
    [HttpGet("days")]
    [ProducesResponseType(typeof(LeaveDaysDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Days(
        [FromQuery] string leaveType, [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] string? fromTime, [FromQuery] string? toTime, CancellationToken ct)
    {
        if (Caller() is not { } caller) return Unauthorized(new { error = SignInAgain });
        var r = await leaves.ComputeDaysAsync(caller, leaveType, from, to, fromTime, toTime, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>The supervisor who will approve a leave of this type and length.</summary>
    [HttpGet("supervisor")]
    [ProducesResponseType(typeof(SupervisorDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Supervisor(
        [FromQuery] string leaveType, [FromQuery] double days, CancellationToken ct)
    {
        if (Caller() is not { } caller) return Unauthorized(new { error = SignInAgain });
        var r = await leaves.GetSupervisorAsync(caller, leaveType, days, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>
    /// Submit a leave request (multipart form; attach a medical certificate or
    /// other files in <c>files</c>). <c>success=false</c> in the body means
    /// SmartHCM refused it — <c>message</c> says why (e.g. overlapping leave).
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(60 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 60 * 1024 * 1024)]
    [ProducesResponseType(typeof(LeaveWriteResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Apply([FromForm] ApplyLeaveForm form, CancellationToken ct)
    {
        if (Caller() is not { } caller) return Unauthorized(new { error = SignInAgain });

        var attachments = new List<LeaveAttachment>();
        foreach (var file in form.Files ?? [])
        {
            if (file.Length > LeaveRequestService.MaxAttachmentBytes)
                return BadRequest(new { error = $"\"{file.FileName}\" is larger than 10 MB." });
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            attachments.Add(new LeaveAttachment(
                Path.GetFileName(file.FileName), file.ContentType ?? "application/octet-stream", ms.ToArray()));
        }

        var r = await leaves.ApplyAsync(caller, new ApplyLeaveCommand(
            form.LeaveType ?? string.Empty, form.FromDate, form.ToDate, form.NoOfDays,
            form.Remarks, form.FromTime, form.ToTime, attachments), ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>The signed-in employee's own leave requests (every status).</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveRequestRowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (Caller() is not { } caller) return Unauthorized(new { error = SignInAgain });
        var r = await leaves.GetMyRequestsAsync(caller, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Other employees' pending requests awaiting the signed-in user's decision.</summary>
    [HttpGet("approvals")]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveRequestRowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approvals(CancellationToken ct)
    {
        if (Caller() is not { } caller) return Unauthorized(new { error = SignInAgain });
        var r = await leaves.GetApprovalQueueAsync(caller, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Full detail (remarks etc.) of one request.</summary>
    [HttpGet("{rflNo}")]
    [ProducesResponseType(typeof(IReadOnlyDictionary<string, string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Detail(string rflNo, [FromQuery] string? applicantId, CancellationToken ct)
    {
        if (Caller() is not { } caller) return Unauthorized(new { error = SignInAgain });
        var r = await leaves.GetDetailAsync(caller, rflNo, applicantId, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>
    /// Approve (<c>A</c>), reject (<c>R</c>) or withdraw (<c>C</c>) a pending
    /// request. <c>success=false</c> in the body means SmartHCM refused it.
    /// </summary>
    [HttpPost("{rflNo}/action")]
    [ProducesResponseType(typeof(LeaveWriteResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> PerformAction(string rflNo, [FromBody] LeaveActionRequest body, CancellationToken ct)
    {
        if (Caller() is not { } caller) return Unauthorized(new { error = SignInAgain });
        var r = await leaves.PerformActionAsync(caller, rflNo, body.Action, body.Remarks, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    private SmartHcmCredential? Caller()
    {
        var header = Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : string.Empty;
        return credentials.Find(token);
    }
}

/// <summary>Multipart form for <c>POST /api/leave-requests</c>.</summary>
public sealed class ApplyLeaveForm
{
    public string? LeaveType { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public double NoOfDays { get; set; }
    public string? Remarks { get; set; }
    public string? FromTime { get; set; }
    public string? ToTime { get; set; }
    public List<IFormFile>? Files { get; set; }
}

/// <summary>Body for <c>POST /api/leave-requests/{rflNo}/action</c>.</summary>
public sealed record LeaveActionRequest(string Action, string? Remarks);
