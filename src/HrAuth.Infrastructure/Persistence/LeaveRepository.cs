using HrAuth.Application.Leaves.Dtos;
using HrAuth.Application.Persistence;
using HrAuth.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrAuth.Infrastructure.Persistence;

/// <summary><see cref="ILeaveRepository"/> backed by EF Core / PostgreSQL.</summary>
public sealed class LeaveRepository(HrAuthDbContext db) : ILeaveRepository
{
    public async Task<int> ReplaceForEmployeeAsync(
        string employeeId, IReadOnlyCollection<LeaveDto> leaves,
        CancellationToken ct = default)
    {
        // Replace the employee's stored leaves so cancellations/edits propagate.
        await db.Leaves.Where(l => l.EmployeeId == employeeId).ExecuteDeleteAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var l in leaves)
        {
            db.Leaves.Add(new LeaveEntity
            {
                EmployeeId = l.EmployeeId,
                EmployeeName = l.EmployeeName,
                LeaveType = l.LeaveType,
                LeaveName = l.LeaveName,
                FromDate = l.FromDate,
                ToDate = l.ToDate,
                Status = l.Status,
                StatusId = l.StatusId,
                DayHourRequested = l.DayHourRequested,
                DayType = l.DayType,
                FetchedUtc = now,
            });
        }

        await db.SaveChangesAsync(ct);
        return leaves.Count;
    }

    public async Task<IReadOnlyList<LeaveDto>> ListAsync(
        string employeeId, CancellationToken ct = default)
        => await db.Leaves
            .AsNoTracking()
            .Where(l => l.EmployeeId == employeeId)
            .OrderByDescending(l => l.FromDate)
            .Select(l => Map(l))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LeaveDto>> GetApprovedOverlappingAsync(
        IReadOnlyCollection<string> employeeIds, DateOnly from, DateOnly to,
        CancellationToken ct = default)
    {
        if (employeeIds.Count == 0)
            return [];

        return await db.Leaves
            .AsNoTracking()
            .Where(l => employeeIds.Contains(l.EmployeeId)
                        && l.StatusId == "A"
                        && l.FromDate <= to && l.ToDate >= from)
            .Select(l => Map(l))
            .ToListAsync(ct);
    }

    private static LeaveDto Map(LeaveEntity l) => new(
        l.EmployeeId, l.EmployeeName, l.LeaveType, l.LeaveName,
        l.FromDate, l.ToDate, l.Status, l.StatusId,
        l.DayHourRequested, l.DayType);
}
