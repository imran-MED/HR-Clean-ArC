using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrAuth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncProfileAndRosterCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Position / SupervisorEmployeeId (and its index) were introduced by
            // an earlier migration whose file was lost; they already exist on the
            // live DB. Add them idempotently so this migration is a no-op there
            // yet still provisions a brand-new database.
            migrationBuilder.Sql(
                "ALTER TABLE employees ADD COLUMN IF NOT EXISTS \"Position\" character varying(128) NULL;");
            migrationBuilder.Sql(
                "ALTER TABLE employees ADD COLUMN IF NOT EXISTS \"SupervisorEmployeeId\" character varying(32) NULL;");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_employees_SupervisorEmployeeId\" "
                + "ON employees (\"SupervisorEmployeeId\");");

            migrationBuilder.CreateTable(
                name: "roster_cache",
                columns: table => new
                {
                    EmployeeId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttendanceDate = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RosterJson = table.Column<string>(type: "jsonb", nullable: false),
                    FetchedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roster_cache", x => new { x.EmployeeId, x.AttendanceDate });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only undo what this migration genuinely introduced — the roster
            // cache. The employee columns predate it and are left intact.
            migrationBuilder.DropTable(
                name: "roster_cache");
        }
    }
}
