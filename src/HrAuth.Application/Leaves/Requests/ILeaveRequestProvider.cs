using HrAuth.Application.Abstractions;
using HrAuth.Application.Common;
using HrAuth.Application.Leaves.Requests.Dtos;

namespace HrAuth.Application.Leaves.Requests;

/// <summary>
/// Port for the SmartHCM leave workflow — the same ASMX operations the Dawam
/// mobile app calls. Every call acts as the signed-in user
/// (<paramref name="caller"/>), so SmartHCM applies its own permissions.
/// </summary>
public interface ILeaveRequestProvider
{
    /// <summary><c>GetLeaveTypes</c> — leave types the employee may apply for.</summary>
    Task<Result<IReadOnlyList<LeaveTypeDto>>> GetLeaveTypesAsync(
        SmartHcmCredential caller, CancellationToken ct = default);

    /// <summary><c>GET_TO_DATE</c> — effective number of days for a date range.</summary>
    Task<Result<LeaveDaysDto>> ComputeDaysAsync(
        SmartHcmCredential caller, string leaveTypeId, DateOnly from, DateOnly to,
        string? fromTime, string? toTime, CancellationToken ct = default);

    /// <summary><c>Leave_getSupervisorName</c> — who will approve this leave.</summary>
    Task<Result<SupervisorDto>> GetSupervisorAsync(
        SmartHcmCredential caller, string leaveTypeId, double days, string? rflNo,
        CancellationToken ct = default);

    /// <summary><c>SaveLeave</c> — submit a request (attachments in the same call).</summary>
    Task<Result<LeaveWriteResultDto>> SubmitAsync(
        SmartHcmCredential caller, ApplyLeaveCommand command, CancellationToken ct = default);

    /// <summary>
    /// <c>Hrs_GetPendingLeaves</c> — everything the caller can see: their own
    /// requests (all statuses) plus other people's while pending or rejected.
    /// </summary>
    Task<Result<IReadOnlyList<LeaveRequestRowDto>>> ListVisibleAsync(
        SmartHcmCredential caller, CancellationToken ct = default);

    /// <summary><c>Hrs_GetLeaveDetail</c> — full detail (remarks etc.) of one request.</summary>
    Task<Result<IReadOnlyDictionary<string, string>>> GetDetailAsync(
        SmartHcmCredential caller, string rflNo, string? applicantId,
        CancellationToken ct = default);

    /// <summary>
    /// <c>Hrs_Leave_PerformAction</c> — approve ('A'), reject ('R') or
    /// withdraw ('C') a request as the caller.
    /// </summary>
    Task<Result<LeaveWriteResultDto>> PerformActionAsync(
        SmartHcmCredential caller, string rflNo, string action, string? remarks,
        string approvedDays, CancellationToken ct = default);
}
