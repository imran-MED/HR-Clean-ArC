using HrAuth.Application.Common;
using HrAuth.Application.Leaves.Dtos;
using HrAuth.Application.Persistence;

namespace HrAuth.Application.Leaves;

/// <summary>Default <see cref="ILeaveService"/>.</summary>
public sealed class LeaveService(
    ILeaveProvider provider,
    ILeaveRepository repository) : ILeaveService
{
    public async Task<Result<IReadOnlyList<LeaveDto>>> FetchAndStoreAsync(
        string employeeId, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<IReadOnlyList<LeaveDto>>.Failure("employeeId is required.");

        var fetched = await provider.GetLeavesAsync(employeeId, password, ct);
        if (!fetched.IsSuccess || fetched.Value is null)
            return fetched;

        // Best-effort persistence — never fail the fetch on a DB error.
        try
        {
            await repository.ReplaceForEmployeeAsync(employeeId, fetched.Value, ct);
        }
        catch
        {
            // ignore — persistence is optional
        }

        return fetched;
    }

    public Task<IReadOnlyList<LeaveDto>> ListStoredAsync(
        string employeeId, CancellationToken ct = default)
        => repository.ListAsync(employeeId, ct);
}
