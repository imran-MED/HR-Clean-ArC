using HrAuth.Application.Attendance.Dtos;

namespace HrAuth.Application.Persistence;

/// <summary>Persistence port for roster-reconciliation runs.</summary>
public interface IReconciliationRepository
{
    /// <summary>Persist a completed reconciliation run; returns its id.</summary>
    Task<Guid> SaveRunAsync(
        AttendanceReconciliationResult result, CancellationToken ct = default);
}
