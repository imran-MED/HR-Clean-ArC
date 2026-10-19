using HrAuth.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrAuth.Infrastructure.Persistence;

/// <summary>EF Core context for the HrAuth PostgreSQL database.</summary>
public sealed class HrAuthDbContext(DbContextOptions<HrAuthDbContext> options)
    : DbContext(options)
{
    public DbSet<EmployeeEntity> Employees => Set<EmployeeEntity>();

    public DbSet<AttendanceRecordEntity> AttendanceRecords
        => Set<AttendanceRecordEntity>();

    public DbSet<ReconciliationRunEntity> ReconciliationRuns
        => Set<ReconciliationRunEntity>();

    public DbSet<RoleEntity> Roles => Set<RoleEntity>();

    public DbSet<RosterCacheEntity> RosterCache => Set<RosterCacheEntity>();

    public DbSet<LeaveEntity> Leaves => Set<LeaveEntity>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<EmployeeEntity>(e =>
        {
            e.ToTable("employees");
            e.HasKey(x => x.EmployeeId);
            e.Property(x => x.EmployeeId).HasMaxLength(32);
            e.Property(x => x.UserName).HasMaxLength(128);
            e.Property(x => x.DisplayName).HasMaxLength(256);
            e.Property(x => x.Position).HasMaxLength(128);
            e.Property(x => x.SupervisorEmployeeId).HasMaxLength(32);
            e.HasIndex(x => x.SupervisorEmployeeId);
            e.Property(x => x.Roles).HasColumnType("text[]");
        });

        model.Entity<AttendanceRecordEntity>(e =>
        {
            e.ToTable("attendance_records");
            e.HasKey(x => x.Id);
            e.Property(x => x.EmployeeId).HasMaxLength(32);
            e.Property(x => x.RawEmployeeId).HasMaxLength(32);
            e.Property(x => x.SourceFile).HasMaxLength(260);
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.Timestamp);
        });

        model.Entity<ReconciliationRunEntity>(e =>
        {
            e.ToTable("reconciliation_runs");
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).HasMaxLength(260);
            e.Property(x => x.DaysJson).HasColumnType("jsonb");
            e.HasIndex(x => x.CreatedUtc);
        });

        model.Entity<RoleEntity>(e =>
        {
            e.ToTable("roles");
            e.HasKey(x => x.Name);
            e.Property(x => x.Name).HasMaxLength(64);
            e.Property(x => x.Description).HasMaxLength(256);
        });

        model.Entity<RosterCacheEntity>(e =>
        {
            e.ToTable("roster_cache");
            e.HasKey(x => new { x.EmployeeId, x.AttendanceDate });
            e.Property(x => x.EmployeeId).HasMaxLength(32);
            e.Property(x => x.AttendanceDate).HasMaxLength(10);
            e.Property(x => x.RosterJson).HasColumnType("jsonb");
        });

        model.Entity<LeaveEntity>(e =>
        {
            e.ToTable("leaves");
            e.HasKey(x => x.Id);
            e.Property(x => x.EmployeeId).HasMaxLength(32);
            e.Property(x => x.EmployeeName).HasMaxLength(256);
            e.Property(x => x.LeaveType).HasMaxLength(32);
            e.Property(x => x.LeaveName).HasMaxLength(128);
            e.Property(x => x.Status).HasMaxLength(64);
            e.Property(x => x.StatusId).HasMaxLength(8);
            e.Property(x => x.DayType).HasMaxLength(4);
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => new { x.FromDate, x.ToDate });
        });
    }
}
