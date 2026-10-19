using HrAuth.Application.Common;
using HrAuth.Application.Employees;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrAuth.Infrastructure.SmartHcm;

/// <summary>
/// <see cref="IEmployeeDirectoryProvider"/> that sources <b>Position</b> and
/// <b>Supervisor</b> from the same ASMX methods the Dawam mobile app's admin
/// dashboard uses — reachable over the public internet with a single
/// manager/admin credential, with no per-employee passwords, no security code,
/// and no Oracle network dependency:
/// <list type="bullet">
///   <item><c>MobGetEmployeeList</c> — the manager's employee list (incl.
///   designation/position), cached as a directory snapshot.</item>
///   <item><c>Leave_getSupervisorName</c> — the leave-workflow supervisor (best
///   effort; needs a leave-type context).</item>
/// </list>
/// </summary>
public sealed class SmartHcmApiDirectoryProvider(
    SmartHcmClient client,
    SmartHcmDirectoryCache cache,
    IOptions<SmartHcmOptions> options,
    ILogger<SmartHcmApiDirectoryProvider> logger) : IEmployeeDirectoryProvider
{
    private readonly SmartHcmOptions _options = options.Value;

    // Candidate column names for the designation/position field — the exact
    // name comes from the Oracle SP ess_mobile_pkg.mob_get_employee_list and is
    // confirmed at runtime (logged on first load).
    private static readonly string[] PositionColumns =
    {
        "DESIGNATION", "DESIGNATION_NAME", "DESIG_NAME", "DESIG",
        "POSITION", "POSITION_NAME", "JOB_TITLE", "TITLE",
    };

    private static readonly string[] EmployeeIdColumns =
    {
        "EMPLOYEE_ID", "EMP_ID", "EMPLOYEEID", "ID",
    };

    public async Task<Result<SmartHcmEmployeeInfo>> GetEmployeeInfoAsync(
        string employeeId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.DirectoryAdminEmpId) ||
            string.IsNullOrWhiteSpace(_options.DirectoryAdminPassword))
        {
            return Result<SmartHcmEmployeeInfo>.Failure(
                "SmartHCM API directory is not configured: set "
                + "SmartHcm:DirectoryAdminEmpId and SmartHcm:DirectoryAdminPassword.");
        }

        string? position = null;
        try
        {
            var snapshot = await cache.GetOrLoadAsync(LoadDirectoryAsync, ct);
            if (snapshot.TryGetValue(Key(employeeId), out var info))
                position = info.Position;
            else
                logger.LogWarning(
                    "SmartHCM directory snapshot has no row for {EmployeeId}.", employeeId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MobGetEmployeeList directory load failed.");
            return Result<SmartHcmEmployeeInfo>.Failure(
                $"Could not reach the SmartHCM directory service: {ex.Message}");
        }

        // Supervisor (best-effort) via the leave-workflow lookup.
        var (supervisorId, supervisorName) = await TryGetSupervisorAsync(employeeId, ct);

        logger.LogInformation(
            "SmartHCM (API) info for {EmployeeId}: position={Position}, supervisor={SupervisorId}",
            employeeId, position ?? "—", supervisorId ?? "—");

        return Result<SmartHcmEmployeeInfo>.Success(
            new SmartHcmEmployeeInfo(position, supervisorId, supervisorName));
    }

    /// <summary>
    /// Pull the admin's employee list once and project it into a
    /// position-by-employee snapshot.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, SmartHcmEmployeeInfo>> LoadDirectoryAsync(
        CancellationToken ct)
    {
        var xml = await client.CallAsync(
            "MobGetEmployeeList",
            new Dictionary<string, string>
            {
                ["p_emp_id"] = _options.DirectoryAdminEmpId,
                ["Password"] = SmartHcmCrypto.HashPassword(_options.DirectoryAdminPassword),
                ["langCode"] = string.IsNullOrWhiteSpace(_options.LanguageCode)
                    ? "en-US" : _options.LanguageCode,
                ["TokenKey"] = _options.TokenKey,
                ["p_registered"] = "0",
            },
            ct);

        var rows = SmartHcmClient.ParseJsonRows(xml);
        var map = new Dictionary<string, SmartHcmEmployeeInfo>(StringComparer.Ordinal);

        if (rows.Count == 0)
        {
            logger.LogWarning(
                "MobGetEmployeeList returned no rows (check admin credential / token).");
            return map;
        }

        // Log the available columns once so the designation column can be
        // confirmed against the live response.
        logger.LogInformation(
            "MobGetEmployeeList returned {Count} rows; columns: {Columns}",
            rows.Count, string.Join(", ", rows[0].Keys));

        foreach (var row in rows)
        {
            var id = Pick(row, EmployeeIdColumns);
            if (string.IsNullOrWhiteSpace(id))
                continue;
            var position = Clean(Pick(row, PositionColumns));
            map[Key(id)] = new SmartHcmEmployeeInfo(position, null, null);
        }
        return map;
    }

    private async Task<(string? Id, string? Name)> TryGetSupervisorAsync(
        string employeeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SupervisorLeaveType))
            return (null, null);

        try
        {
            var xml = await client.CallAsync(
                "Leave_getSupervisorName",
                new Dictionary<string, string>
                {
                    ["p_rflno"] = string.Empty,
                    ["p_applicant"] = employeeId,
                    ["p_leave_type"] = _options.SupervisorLeaveType,
                    ["p_appr_days"] = _options.SupervisorAppliedDays,
                    ["p_emp_id"] = employeeId,
                    ["Password"] = SmartHcmCrypto.HashPassword(_options.DirectoryAdminPassword),
                    ["TokenKey"] = _options.TokenKey,
                },
                ct);

            var rows = SmartHcmClient.ParseDataTable(xml);
            if (rows.Count == 0)
                return (null, null);

            var row = rows[0];
            var id = Clean(Pick(row,
                ["SupervisorId", "SUPERVISOR_ID", "APPROVER_ID", "EMP_ID", "EmployeeId"]));
            var name = Clean(Pick(row,
                ["SupervisorName", "SUPERVISOR_NAME", "EMP_NAME", "Name", "FullName"]));
            return (id, name);
        }
        catch (Exception ex)
        {
            // Supervisor is best-effort — never lose the resolved position.
            logger.LogWarning(ex, "Leave_getSupervisorName failed for {EmployeeId}", employeeId);
            return (null, null);
        }
    }

    private static string? Pick(IReadOnlyDictionary<string, string> row, string[] keys)
    {
        foreach (var key in keys)
            if (row.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                return v.Trim();
        return null;
    }

    /// <summary>Normalize an id for matching (tolerate leading-zero variance).</summary>
    private static string Key(string id)
    {
        var t = id.Trim();
        return long.TryParse(t, out var n) ? n.ToString() : t;
    }

    private static string? Clean(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim();
        return t.Equals("null", StringComparison.OrdinalIgnoreCase) ? null : t;
    }
}
