using HrAuth.Application.Attendance.Dtos;
using HrAuth.Application.Persistence;
using HrAuth.Infrastructure.Persistence.Entities;

namespace HrAuth.Infrastructure.Persistence;

/// <summary><see cref="IAttendanceRepository"/> backed by EF Core / PostgreSQL.</summary>
public sealed class AttendanceRepository(HrAuthDbContext db) : IAttendanceRepository
{
    public async Task SaveRecordsAsync(
        string sourceFile, IReadOnlyList<AttendanceRecordDto> records,
        CancellationToken ct = default)
    {
        if (records.Count == 0)
            return;

        var importedUtc = DateTime.UtcNow;
        db.AttendanceRecords.AddRange(records.Select(r => new AttendanceRecordEntity
        {
            EmployeeId = r.EmployeeId,
            RawEmployeeId = r.RawEmployeeId,
            Timestamp = r.Timestamp,
            Status = r.Status,
            Verify = r.Verify,
            WorkCode = r.WorkCode,
            Reserved = r.Reserved,
            SourceFile = sourceFile,
            ImportedUtc = importedUtc,
        }));

        await db.SaveChangesAsync(ct);
    }
}
