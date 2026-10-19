using HrAuth.Application.Leaves.Dtos;

namespace HrAuth.Application.Persistence;

/// <summary>
/// Persistence port for stored employee leaves. Implemented in Infrastructure
/// (EF Core / PostgreSQL).
/// </summary>
public interface ILeaveRepository
{
    /// <summary>
    /// Replace an employee's stored leaves with the supplied set (so a re-fetch
    /// reflects cancellations). Returns the number of rows written.
    /// </summary>
    Task<int> ReplaceForEmployeeAsync(
        string employeeId, IReadOnlyCollection<LeaveDto> leaves,
        CancellationToken ct = default);

    /// <summary>All stored leaves for one employee, newest first.</summary>
    Task<IReadOnlyList<LeaveDto>> ListAsync(
        string employeeId, CancellationToken ct = default);

    /// <summary>
    /// Approved leaves for the given employees overlapping [from, to] — used by
    /// reconciliation to mark days on leave instead of missing.
    /// </summary>
    Task<IReadOnlyList<LeaveDto>> GetApprovedOverlappingAsync(
        IReadOnlyCollection<string> employeeIds, DateOnly from, DateOnly to,
        CancellationToken ct = default);
}
