using System.Xml.Linq;
using HrAuth.Application.Common;
using HrAuth.Application.Employees;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;

namespace HrAuth.Infrastructure.SmartHcm;

/// <summary>
/// <see cref="IEmployeeDirectoryProvider"/> that reads <b>Position</b> straight
/// from the SmartHCM Oracle database via the <c>GET_POSITION_NAME</c> function —
/// the same approach as HaramAPI's <c>HrEmployeeService</c>. This bypasses the
/// asmx <c>*_SecurityCode</c> methods (and their security code) entirely.
///
/// The immediate <b>supervisor</b> has no direct column — in SmartHCM it is the
/// leave-workflow parent node — so it is resolved (best-effort) through the asmx
/// <c>GetSuperVisor</c> operation, which needs no security code, only a
/// leave-type context (<c>SmartHcm:SupervisorLeaveType</c>). When no leave type
/// is configured, only Position is populated.
/// </summary>
public sealed class OracleEmployeeDirectoryProvider(
    IConfiguration configuration,
    SmartHcmClient client,
    IOptions<SmartHcmOptions> options,
    ILogger<OracleEmployeeDirectoryProvider> logger) : IEmployeeDirectoryProvider
{
    private readonly SmartHcmOptions _options = options.Value;
    private readonly string _connectionString =
        configuration.GetConnectionString("Oracle") ?? string.Empty;

    // P_FLAG => 'E' returns the English/primary position name; P_LANG selects the
    // secondary-language label. Filtering by employee_id also validates the id.
    private const string PositionSql =
        "SELECT GET_POSITION_NAME(ep.employee_id, P_FLAG => 'E', P_LANG => :P_LANG_CODE) "
        + "AS position_name "
        + "FROM hrs_employee_personnel ep "
        + "WHERE ep.employee_id = :P_EMP_ID";

    public async Task<Result<SmartHcmEmployeeInfo>> GetEmployeeInfoAsync(
        string employeeId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return Result<SmartHcmEmployeeInfo>.Failure(
                "Oracle connection string is not configured (set ConnectionStrings:Oracle).");
        }

        var langCode = string.IsNullOrWhiteSpace(_options.LanguageCode)
            ? "en-US" : _options.LanguageCode;

        string? position;
        try
        {
            await using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = PositionSql;
            command.BindByName = true;
            command.Parameters.Add(new OracleParameter("P_LANG_CODE", OracleDbType.Varchar2) { Value = langCode });
            command.Parameters.Add(new OracleParameter("P_EMP_ID", OracleDbType.Varchar2) { Value = employeeId });

            var raw = await command.ExecuteScalarAsync(ct);
            position = Clean(raw?.ToString());

            if (raw is null)
            {
                // No row at all → the employee id does not exist in SmartHCM.
                logger.LogWarning(
                    "SmartHCM Oracle returned no employee row for {EmployeeId}.", employeeId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SmartHCM Oracle position lookup failed for {EmployeeId}", employeeId);
            return Result<SmartHcmEmployeeInfo>.Failure(
                $"Could not reach the SmartHCM Oracle database: {ex.Message}");
        }

        // Supervisor (best-effort) via the asmx GetSuperVisor — no security code,
        // only a leave-type context.
        string? supervisorId = null, supervisorName = null;
        if (!string.IsNullOrWhiteSpace(_options.SupervisorLeaveType))
        {
            try
            {
                var xml = await client.CallAsync(
                    "GetSuperVisor",
                    new Dictionary<string, string>
                    {
                        ["empId"] = employeeId,
                        ["appliedDays"] = _options.SupervisorAppliedDays,
                        ["leave_type"] = _options.SupervisorLeaveType,
                    },
                    ct);

                var doc = XDocument.Parse(xml);
                supervisorId = Clean(LocalValue(doc, "SupervisorId"));
                supervisorName = Clean(LocalValue(doc, "SupervisorName"));
            }
            catch (Exception ex)
            {
                // Supervisor is best-effort — a failure here must not lose the
                // position we already resolved from Oracle.
                logger.LogWarning(ex, "SmartHCM GetSuperVisor failed for {EmployeeId}", employeeId);
            }
        }

        logger.LogInformation(
            "SmartHCM (Oracle) info for {EmployeeId}: position={Position}, supervisor={SupervisorId}",
            employeeId, position ?? "—", supervisorId ?? "—");

        return Result<SmartHcmEmployeeInfo>.Success(
            new SmartHcmEmployeeInfo(position, supervisorId, supervisorName));
    }

    private static string? LocalValue(XDocument doc, string localName)
    {
        var el = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);
        var v = el?.Value;
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    private static string? Clean(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim();
        // SmartHCM commonly returns the literal "null" string for empties.
        return t.Equals("null", StringComparison.OrdinalIgnoreCase) ? null : t;
    }
}
