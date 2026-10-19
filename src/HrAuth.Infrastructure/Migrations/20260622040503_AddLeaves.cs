using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HrAuth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "leaves",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EmployeeName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LeaveType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LeaveName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StatusId = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    DayHourRequested = table.Column<double>(type: "double precision", nullable: true),
                    DayType = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    FetchedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leaves", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_leaves_EmployeeId",
                table: "leaves",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_leaves_FromDate_ToDate",
                table: "leaves",
                columns: new[] { "FromDate", "ToDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "leaves");
        }
    }
}
