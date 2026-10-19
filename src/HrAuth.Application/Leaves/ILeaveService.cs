using HrAuth.Application.Common;
using HrAuth.Application.Leaves.Dtos;

namespace HrAuth.Application.Leaves;

/// <summary>Fetch-and-store plus read operations for employee leaves.</summary>
public interface ILeaveService
{
    /// <summary>
    /// Fetch an employee's leaves from SmartHCM and store them (replacing any
    /// previously stored ones), returning the fetched leaves.
    /// </summary>
    Task<Result<IReadOnlyList<LeaveDto>>> FetchAndStoreAsync(
        string employeeId, string password, CancellationToken ct = default);

    /// <summary>List the leaves already stored for an employee.</summary>
    Task<IReadOnlyList<LeaveDto>> ListStoredAsync(
        string employeeId, CancellationToken ct = default);
}
