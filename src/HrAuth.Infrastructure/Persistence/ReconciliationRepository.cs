using System.Text.Json;
using HrAuth.Application.Attendance.Dtos;
using HrAuth.Application.Persistence;
using HrAuth.Infrastructure.Persistence.Entities;

namespace HrAuth.Infrastructure.Persistence;

/// <summary>
/// <see cref="IReconciliationRepository"/> backed by EF Core / PostgreSQL.
/// </summary>
public sealed class ReconciliationRepository(HrAuthDbContext db)
    : IReconciliationRepository
{
    public async Task<Guid> SaveRunAsync(
        AttendanceReconciliationResult result, CancellationToken ct = default)
    {
        var run = new ReconciliationRunEntity
        {
            Id = Guid.NewGuid(),
            FileName = result.FileName,
            CreatedUtc = DateTime.UtcNow,
            TotalLines = result.TotalLines,
            ImportedCount = result.ImportedCount,
            SkippedCount = result.SkippedCount,
            TotalMissingPeriods = result.TotalMissingPeriods,
            EmployeesWithoutRoster = result.EmployeesWithoutRoster,
            TotalLateMinutes = result.TotalLateMinutes,
            TotalEarlyDepartureMinutes = result.TotalEarlyDepartureMinutes,
            DaysJson = JsonSerializer.Serialize(result.Days),
        };

        db.ReconciliationRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run.Id;
    }
}
