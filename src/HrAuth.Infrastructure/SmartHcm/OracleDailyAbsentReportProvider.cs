using HrAuth.Application.Common;
using HrAuth.Application.Reports;
using HrAuth.Application.Reports.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace HrAuth.Infrastructure.SmartHcm;

/// <summary>
/// <see cref="IDailyAbsentReportProvider"/> running the
/// piv_teaching_staff_admin_absent report inline against the SmartHCM Oracle
/// schema (so the view does not need to exist in the target database).
/// Teaching staff / admin = positions 118, 119, 120 with employee type 361;
/// an absence is any period whose attendance status is neither P nor L.
/// Periods 1–3 group into GRP_MORNING, 4–6 into GRP_EVENING, others report
/// per-period — one row per employee / day / group.
/// </summary>
public sealed class OracleDailyAbsentReportProvider(
    IConfiguration configuration,
    ILogger<OracleDailyAbsentReportProvider> logger) : IDailyAbsentReportProvider
{
    private readonly string _connectionString =
        configuration.GetConnectionString("Oracle") ?? string.Empty;

    private const string ReportSql = """
        SELECT TRUNC(a.att_date)                    attend_date,
               a.employee_id                        employee_id,
               MAX(get_emp_name(a.employee_id))     employee_name,
               c.title                              position,
               d.l_desc                             department,
               CASE
                 WHEN a.period_id IN (1,2,3) THEN 'GRP_MORNING'
                 WHEN a.period_id IN (4,5,6) THEN 'GRP_EVENING'
                 ELSE 'P_' || a.period_id
               END                                  group_key,
               LISTAGG(DISTINCT a.short_name, '+')
                 WITHIN GROUP (ORDER BY a.short_name) periods,
               SUM(a.required_hours)                required_hours,
               SUM(a.present_hours)                 present_hours,
               SUM(a.leave_hours)                   leave_hours,
               MIN(a.attendance_status)             attendance_status
          FROM tom_period_att_with_leave_vw a,
               hrs_employee_personnel b,
               hrs_position c,
               set_location d
         WHERE a.employee_id = b.employee_id
           AND c.position_id = b.position_id
           AND d.loc_id      = b.org_loc
           AND c.position_id IN (118, 119, 120)
           AND b.type_id = 361
           AND a.attendance_status NOT IN ('P', 'L')
           AND a.att_date >= :P_FROM
           AND a.att_date <  :P_TO + 1
           AND (:P_EMP_ID IS NULL OR a.employee_id = :P_EMP_ID)
         GROUP BY TRUNC(a.att_date),
                  a.employee_id,
                  c.title,
                  d.l_desc,
                  CASE
                    WHEN a.period_id IN (1,2,3) THEN 'GRP_MORNING'
                    WHEN a.period_id IN (4,5,6) THEN 'GRP_EVENING'
                    ELSE 'P_' || a.period_id
                  END
         ORDER BY employee_name, attend_date, group_key
        """;

    public async Task<Result<IReadOnlyList<DailyAbsentRowDto>>> GetAsync(
        DateOnly from, DateOnly to, string? employeeId, string? bearerToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return Result<IReadOnlyList<DailyAbsentRowDto>>.Failure(
                "Oracle connection string is not configured (set ConnectionStrings:Oracle).");
        }

        var rows = new List<DailyAbsentRowDto>();
        try
        {
            await using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = ReportSql;
            command.BindByName = true;
            command.Parameters.Add(new OracleParameter("P_FROM", OracleDbType.Date)
            { Value = from.ToDateTime(TimeOnly.MinValue) });
            command.Parameters.Add(new OracleParameter("P_TO", OracleDbType.Date)
            { Value = to.ToDateTime(TimeOnly.MinValue) });
            command.Parameters.Add(new OracleParameter("P_EMP_ID", OracleDbType.Varchar2)
            { Value = string.IsNullOrWhiteSpace(employeeId) ? DBNull.Value : employeeId });

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var attendDate = reader["ATTEND_DATE"] is DateTime d ? d : default;
                rows.Add(new DailyAbsentRowDto
                {
                    AttendDate = attendDate.ToString("yyyy-MM-dd"),
                    EmployeeId = reader["EMPLOYEE_ID"]?.ToString()?.Trim() ?? string.Empty,
                    EmployeeName = reader["EMPLOYEE_NAME"]?.ToString()?.Trim(),
                    Position = reader["POSITION"]?.ToString()?.Trim(),
                    Department = reader["DEPARTMENT"]?.ToString()?.Trim(),
                    GroupKey = reader["GROUP_KEY"]?.ToString() ?? string.Empty,
                    Periods = reader["PERIODS"]?.ToString(),
                    RequiredHours = reader["REQUIRED_HOURS"] as decimal? ?? 0m,
                    PresentHours = reader["PRESENT_HOURS"] as decimal? ?? 0m,
                    LeaveHours = reader["LEAVE_HOURS"] as decimal? ?? 0m,
                    AttendanceStatus = reader["ATTENDANCE_STATUS"]?.ToString()?.Trim(),
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Daily absent report query failed.");
            return Result<IReadOnlyList<DailyAbsentRowDto>>.Failure(
                $"Could not run the absent report against SmartHCM Oracle: {ex.Message}");
        }

        logger.LogInformation(
            "Daily absent report: {Count} row(s) for {From}..{To} (emp={Emp}).",
            rows.Count, from, to,
            string.IsNullOrWhiteSpace(employeeId) ? "all" : employeeId);

        return Result<IReadOnlyList<DailyAbsentRowDto>>.Success(rows);
    }
}
