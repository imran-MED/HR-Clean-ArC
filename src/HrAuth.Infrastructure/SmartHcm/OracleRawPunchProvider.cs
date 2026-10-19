using System.Globalization;
using System.Text;
using HrAuth.Application.Attendance;
using HrAuth.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace HrAuth.Infrastructure.SmartHcm;

/// <summary>
/// <see cref="IRawPunchProvider"/> reading raw punches straight from SmartHCM's
/// <c>TOM_MACHINE_DATA</c> table — the only source that yields the multiple
/// punches-per-slot needed for direction/duplicate review, in bulk across all
/// employees, with no per-employee password. Each row is one punch:
/// <c>ATT_DATE</c> (date) + <c>TIME_IN_OUT</c> ("HH:mm") = the timestamp, and
/// <c>IS_CHECK_IN</c> ('Y'/'N') is the authoritative direction.
///
/// Punches are emitted in the attendance machine-export layout so the standard
/// importer/reconciler handles them unchanged. Direction maps to the export's
/// <c>Status</c> column: check-in → 0, check-out → 1.
/// </summary>
public sealed class OracleRawPunchProvider(
    IConfiguration configuration,
    ILogger<OracleRawPunchProvider> logger) : IRawPunchProvider
{
    private readonly string _connectionString =
        configuration.GetConnectionString("Oracle") ?? string.Empty;

    private const string PunchSql =
        "SELECT md.EMPLOYEE_ID, md.ATT_DATE, md.TIME_IN_OUT, md.IS_CHECK_IN, "
        + "md.MACHINE_TERMINAL_NO "
        + "FROM TOM_MACHINE_DATA md "
        + "WHERE md.ATT_DATE BETWEEN :P_FROM AND :P_TO "
        + "AND (:P_EMP_ID IS NULL OR md.EMPLOYEE_ID = :P_EMP_ID) "
        + "ORDER BY md.EMPLOYEE_ID, md.ATT_DATE, md.TIME_IN_OUT";

    public async Task<Result<string>> GetPunchFileAsync(
        DateOnly from, DateOnly to, string? employeeId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return Result<string>.Failure(
                "Oracle connection string is not configured (set ConnectionStrings:Oracle).");
        }

        var builder = new StringBuilder();
        var count = 0;
        try
        {
            await using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = PunchSql;
            command.BindByName = true;
            command.Parameters.Add(new OracleParameter("P_FROM", OracleDbType.Date)
            { Value = from.ToDateTime(TimeOnly.MinValue) });
            command.Parameters.Add(new OracleParameter("P_TO", OracleDbType.Date)
            { Value = to.ToDateTime(TimeOnly.MaxValue) });
            command.Parameters.Add(new OracleParameter("P_EMP_ID", OracleDbType.Varchar2)
            { Value = string.IsNullOrWhiteSpace(employeeId) ? DBNull.Value : employeeId });

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var empId = reader["EMPLOYEE_ID"]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(empId))
                    continue;

                var attDate = reader["ATT_DATE"] is DateTime d ? d : default;
                var timeText = reader["TIME_IN_OUT"]?.ToString()?.Trim() ?? string.Empty;
                if (!TimeSpan.TryParseExact(timeText, @"hh\:mm",
                        CultureInfo.InvariantCulture, out var time) &&
                    !TimeSpan.TryParse(timeText, CultureInfo.InvariantCulture, out time))
                {
                    continue; // unreadable time → skip the punch
                }

                var timestamp = attDate.Date + time;
                // IS_CHECK_IN 'Y' = check-in → Status 0; otherwise check-out → 1.
                var isCheckIn = string.Equals(
                    reader["IS_CHECK_IN"]?.ToString()?.Trim(), "Y",
                    StringComparison.OrdinalIgnoreCase);
                var status = isCheckIn ? 0 : 1;

                // EmployeeId  Date  Time  Status  Verify  WorkCode  Reserved
                builder.Append(empId).Append('\t')
                    .Append(timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(status).Append('\t')
                    .Append('1').Append('\t')   // Verify (placeholder)
                    .Append('0').Append('\t')   // WorkCode
                    .Append('0').Append('\n');  // Reserved
                count++;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TOM_MACHINE_DATA punch fetch failed.");
            return Result<string>.Failure(
                $"Could not reach the SmartHCM Oracle database: {ex.Message}");
        }

        logger.LogInformation(
            "Fetched {Count} raw punch(es) from TOM_MACHINE_DATA for {From}..{To} (emp={Emp}).",
            count, from, to, string.IsNullOrWhiteSpace(employeeId) ? "all" : employeeId);

        return Result<string>.Success(builder.ToString());
    }
}
