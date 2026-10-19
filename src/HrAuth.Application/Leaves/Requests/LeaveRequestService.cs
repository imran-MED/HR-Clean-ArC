using System.Globalization;
using HrAuth.Application.Abstractions;
using HrAuth.Application.Common;
using HrAuth.Application.Leaves.Requests.Dtos;

namespace HrAuth.Application.Leaves.Requests;

/// <summary>Apply-for-leave and leave-approval use cases.</summary>
public interface ILeaveRequestService
{
    Task<Result<IReadOnlyList<LeaveTypeDto>>> GetLeaveTypesAsync(
        SmartHcmCredential caller, CancellationToken ct = default);

    Task<Result<LeaveDaysDto>> ComputeDaysAsync(
        SmartHcmCredential caller, string leaveTypeId, DateOnly from, DateOnly to,
        string? fromTime, string? toTime, CancellationToken ct = default);

    Task<Result<SupervisorDto>> GetSupervisorAsync(
        SmartHcmCredential caller, string leaveTypeId, double days,
        CancellationToken ct = default);

    Task<Result<LeaveWriteResultDto>> ApplyAsync(
        SmartHcmCredential caller, ApplyLeaveCommand command, CancellationToken ct = default);

    /// <summary>The caller's own requests, every status, newest first.</summary>
    Task<Result<IReadOnlyList<LeaveRequestRowDto>>> GetMyRequestsAsync(
        SmartHcmCredential caller, CancellationToken ct = default);

    /// <summary>Other employees' requests awaiting the caller's decision.</summary>
    Task<Result<IReadOnlyList<LeaveRequestRowDto>>> GetApprovalQueueAsync(
        SmartHcmCredential caller, CancellationToken ct = default);

    Task<Result<IReadOnlyDictionary<string, string>>> GetDetailAsync(
        SmartHcmCredential caller, string rflNo, string? applicantId,
        CancellationToken ct = default);

    /// <summary>
    /// Approve ('A'), reject ('R') or withdraw ('C') a request. The request is
    /// looked up in the caller's own SmartHCM list first, so the status checks
    /// run on server data — never on what the browser claims.
    /// </summary>
    Task<Result<LeaveWriteResultDto>> PerformActionAsync(
        SmartHcmCredential caller, string rflNo, string action, string? remarks,
        CancellationToken ct = default);
}

/// <summary>Default <see cref="ILeaveRequestService"/>.</summary>
public sealed class LeaveRequestService(ILeaveRequestProvider provider) : ILeaveRequestService
{
    /// <summary>Per-file attachment limit.</summary>
    public const int MaxAttachmentBytes = 10 * 1024 * 1024;

    /// <summary>Maximum number of attachments on one request.</summary>
    public const int MaxAttachments = 5;

    public Task<Result<IReadOnlyList<LeaveTypeDto>>> GetLeaveTypesAsync(
        SmartHcmCredential caller, CancellationToken ct = default)
        => provider.GetLeaveTypesAsync(caller, ct);

    public Task<Result<LeaveDaysDto>> ComputeDaysAsync(
        SmartHcmCredential caller, string leaveTypeId, DateOnly from, DateOnly to,
        string? fromTime, string? toTime, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(leaveTypeId))
            return Task.FromResult(Result<LeaveDaysDto>.Failure("Leave type is required."));
        if (to < from)
            return Task.FromResult(Result<LeaveDaysDto>.Failure(
                "The end date must be on or after the start date."));
        return provider.ComputeDaysAsync(caller, leaveTypeId.Trim(), from, to, fromTime, toTime, ct);
    }

    public Task<Result<SupervisorDto>> GetSupervisorAsync(
        SmartHcmCredential caller, string leaveTypeId, double days,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(leaveTypeId))
            return Task.FromResult(Result<SupervisorDto>.Failure("Leave type is required."));
        return provider.GetSupervisorAsync(caller, leaveTypeId.Trim(), days <= 0 ? 1 : days, null, ct);
    }

    public Task<Result<LeaveWriteResultDto>> ApplyAsync(
        SmartHcmCredential caller, ApplyLeaveCommand command, CancellationToken ct = default)
    {
        string? error = null;
        if (string.IsNullOrWhiteSpace(command.LeaveTypeId))
            error = "Leave type is required.";
        else if (command.ToDate < command.FromDate)
            error = "The end date must be on or after the start date.";
        else if (command.NoOfDays <= 0)
            error = "The number of days must be greater than zero.";
        else if (command.Attachments.Count > MaxAttachments)
            error = $"At most {MaxAttachments} attachments are allowed.";
        else if (command.Attachments.Any(a => a.Content.Length > MaxAttachmentBytes))
            error = $"Each attachment must be {MaxAttachmentBytes / (1024 * 1024)} MB or smaller.";
        else if (command.Attachments.Any(a => a.Content.Length == 0))
            error = "An attachment is empty.";

        return error is null
            ? provider.SubmitAsync(caller, command with { LeaveTypeId = command.LeaveTypeId.Trim() }, ct)
            : Task.FromResult(Result<LeaveWriteResultDto>.Failure(error));
    }

    public async Task<Result<IReadOnlyList<LeaveRequestRowDto>>> GetMyRequestsAsync(
        SmartHcmCredential caller, CancellationToken ct = default)
    {
        var all = await provider.ListVisibleAsync(caller, ct);
        if (!all.IsSuccess || all.Value is null) return all;
        IReadOnlyList<LeaveRequestRowDto> own = all.Value
            .Where(r => r.IsOwn)
            .OrderByDescending(r => r.FromDate)
            .ToList();
        return Result<IReadOnlyList<LeaveRequestRowDto>>.Success(own);
    }

    public async Task<Result<IReadOnlyList<LeaveRequestRowDto>>> GetApprovalQueueAsync(
        SmartHcmCredential caller, CancellationToken ct = default)
    {
        var all = await provider.ListVisibleAsync(caller, ct);
        if (!all.IsSuccess || all.Value is null) return all;
        IReadOnlyList<LeaveRequestRowDto> queue = all.Value
            .Where(r => !r.IsOwn && r.IsPending)
            .OrderBy(r => r.FromDate)
            .ToList();
        return Result<IReadOnlyList<LeaveRequestRowDto>>.Success(queue);
    }

    public Task<Result<IReadOnlyDictionary<string, string>>> GetDetailAsync(
        SmartHcmCredential caller, string rflNo, string? applicantId,
        CancellationToken ct = default)
        => string.IsNullOrWhiteSpace(rflNo)
            ? Task.FromResult(Result<IReadOnlyDictionary<string, string>>.Failure("Request number is required."))
            : provider.GetDetailAsync(caller, rflNo.Trim(), applicantId, ct);

    public async Task<Result<LeaveWriteResultDto>> PerformActionAsync(
        SmartHcmCredential caller, string rflNo, string action, string? remarks,
        CancellationToken ct = default)
    {
        var code = (action ?? string.Empty).Trim().ToUpperInvariant();
        if (code is not (LeaveRequestRules.Approve or LeaveRequestRules.Reject or LeaveRequestRules.Withdraw))
            return Result<LeaveWriteResultDto>.Failure("Action must be A (approve), R (reject) or C (withdraw).");
        if (string.IsNullOrWhiteSpace(rflNo))
            return Result<LeaveWriteResultDto>.Failure("Request number is required.");

        var visible = await provider.ListVisibleAsync(caller, ct);
        if (!visible.IsSuccess || visible.Value is null)
            return Result<LeaveWriteResultDto>.Failure(visible.Error);

        var row = visible.Value.FirstOrDefault(r =>
            string.Equals(r.RflNo?.Trim(), rflNo.Trim(), StringComparison.OrdinalIgnoreCase));
        if (row is null)
            return Result<LeaveWriteResultDto>.Failure(
                "This leave request is not in your SmartHCM list (it may already have been processed).");

        // Withdraw policy (Dawam): only while still awaiting a decision — by the
        // applicant, or by the approver from the queue. Approve/reject likewise
        // only apply to pending requests, and never to your own.
        if (!row.IsPending)
            return Result<LeaveWriteResultDto>.Failure(
                "Only pending requests can be approved, rejected or withdrawn.");
        if (code != LeaveRequestRules.Withdraw && row.IsOwn)
            return Result<LeaveWriteResultDto>.Failure("You cannot approve or reject your own leave.");

        var approvedDays = row.DayHourRequested is { } d
            ? d.ToString("0.##", CultureInfo.InvariantCulture)
            : string.Empty;

        return await provider.PerformActionAsync(caller, row.RflNo!, code, remarks, approvedDays, ct);
    }
}
