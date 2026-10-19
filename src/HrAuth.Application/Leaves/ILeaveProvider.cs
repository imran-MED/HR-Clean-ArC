using HrAuth.Application.Common;
using HrAuth.Application.Leaves.Dtos;

namespace HrAuth.Application.Leaves;

/// <summary>
/// Port for fetching employee leaves from SmartHCM. Implemented in
/// Infrastructure via the <c>Hrs_GetPendingLeaves</c> ASMX operation, which
/// needs the employee's own password (hashed before transmission).
/// </summary>
public interface ILeaveProvider
{
    Task<Result<IReadOnlyList<LeaveDto>>> GetLeavesAsync(
        string employeeId, string password, CancellationToken ct = default);

    /// <summary>
    /// Fetch every leave the configured SmartHCM admin account can see (all
    /// employees), using the admin credential — for bulk reconciliation. Result
    /// is cached briefly. Returns failure when no admin credential is set.
    /// </summary>
    Task<Result<IReadOnlyList<LeaveDto>>> GetAllLeavesAsync(
        CancellationToken ct = default);
}
