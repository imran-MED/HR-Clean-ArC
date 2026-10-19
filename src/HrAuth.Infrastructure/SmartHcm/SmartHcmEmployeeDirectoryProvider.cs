using System.Xml.Linq;
using HrAuth.Application.Common;
using HrAuth.Application.Employees;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrAuth.Infrastructure.SmartHcm;

/// <summary>
/// <see cref="IEmployeeDirectoryProvider"/> backed by the legacy SmartHCM ASMX
/// service. Position comes from <c>GetEmployeeDetailListById_SecurityCode</c>
/// (SP <c>pkg_webservice.ws_employee_detail</c>, position via
/// <c>GET_POSITION_NAME</c>); the immediate supervisor is read from the same
/// detail row when present, otherwise from <c>GetSuperVisor</c>
/// (<c>TheStruct.SupervisorId/SupervisorName</c>).
///
/// The employee-detail response is a serialized ADO.NET DataTable, so column
/// names are matched case-insensitively against a candidate list — the exact
/// SP column names are not guaranteed across SmartHCM builds.
/// </summary>
public sealed class SmartHcmEmployeeDirectoryProvider(
    SmartHcmClient client,
    IOptions<SmartHcmOptions> options,
    ILogger<SmartHcmEmployeeDirectoryProvider> logger) : IEmployeeDirectoryProvider
{
    private readonly SmartHcmOptions _options = options.Value;

    // Candidate column names (case-insensitive) for the detail row.
    private static readonly string[] PositionKeys =
        ["POSITION_NAME", "POSITION", "DESIGNATION", "DESIGNATION_DESCRIPTION",
         "DESIGNATION_NAME", "DESIG", "JOB_TITLE", "JOBTITLE"];
    private static readonly string[] SupervisorIdKeys =
        ["SUPERVISOR_ID", "SUPERVISOR_EMP_ID", "SUPERVISOR_EMPLOYEE_ID",
         "MANAGER_ID", "MANAGER_EMP_ID", "REPORTS_TO", "REPORTING_TO",
         "PARENT_EMP_ID", "SUPERVISOR"];
    private static readonly string[] SupervisorNameKeys =
        ["SUPERVISOR_NAME", "MANAGER_NAME", "REPORTS_TO_NAME", "SUPERVISOR_EMP_NAME"];

    public async Task<Result<SmartHcmEmployeeInfo>> GetEmployeeInfoAsync(
        string employeeId, CancellationToken ct = default)
    {
        // Position is only exposed by the *_SecurityCode methods, so it needs
        // the security code. The supervisor fallback (GetSuperVisor) is NOT
        // gated by any security code — it only needs a leave-type context.
        // Require at least one of the two to be configured.
        var canQueryPosition = !string.IsNullOrWhiteSpace(_options.SecurityKey);
        var canQuerySupervisor = !string.IsNullOrWhiteSpace(_options.SupervisorLeaveType);

        if (!canQueryPosition && !canQuerySupervisor)
        {
            return Result<SmartHcmEmployeeInfo>.Failure(
                "SmartHCM directory sync is not configured: set SmartHcm:SecurityKey "
                + "(for Position) and/or SmartHcm:SupervisorLeaveType (for Supervisor).");
        }

        string? position = null, supervisorId = null, supervisorName = null;

        // ── 1. Employee detail → position (+ supervisor, if the row carries it) ──
        if (canQueryPosition)
        {
            try
            {
                var xml = await client.CallAsync(
                    "GetEmployeeDetailListById_SecurityCode",
                    new Dictionary<string, string>
                    {
                        ["Employee_Id"] = employeeId,
                        ["SecurityKey"] = _options.SecurityKey,
                        ["langCode"] = _options.LanguageCode,
                    },
                    ct);

                var row = FirstRow(xml);
                if (row.Count == 0)
                {
                    logger.LogWarning(
                        "SmartHCM employee detail returned no row for {EmployeeId} "
                        + "(check SecurityKey / employee exists).", employeeId);
                }
                else
                {
                    position = Pick(row, PositionKeys);
                    supervisorId = Pick(row, SupervisorIdKeys);
                    supervisorName = Pick(row, SupervisorNameKeys);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SmartHCM employee detail fetch failed for {EmployeeId}", employeeId);
                return Result<SmartHcmEmployeeInfo>.Failure(
                    $"Could not reach SmartHCM employee detail: {ex.Message}");
            }
        }

        // ── 2. Fallback supervisor via GetSuperVisor (leave-workflow context) ──
        if (string.IsNullOrWhiteSpace(supervisorId) && canQuerySupervisor)
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
                supervisorId = LocalValue(doc, "SupervisorId");
                supervisorName ??= LocalValue(doc, "SupervisorName");
            }
            catch (Exception ex)
            {
                // Supervisor is best-effort — a failure here must not lose the
                // position we already resolved.
                logger.LogWarning(ex, "SmartHCM GetSuperVisor failed for {EmployeeId}", employeeId);
            }
        }

        position = Clean(position);
        supervisorId = Clean(supervisorId);
        supervisorName = Clean(supervisorName);

        logger.LogInformation(
            "SmartHCM info for {EmployeeId}: position={Position}, supervisor={SupervisorId}",
            employeeId, position ?? "—", supervisorId ?? "—");

        return Result<SmartHcmEmployeeInfo>.Success(
            new SmartHcmEmployeeInfo(position, supervisorId, supervisorName));
    }

    // ── XML helpers (namespace-agnostic — match on local element names) ──

    /// <summary>
    /// Read the first data row of a serialized DataTable response into a
    /// case-insensitive column→value map. Skips the inline XSD schema and the
    /// diffgram wrapper; returns the leaf children of the first row element.
    /// </summary>
    private static Dictionary<string, string> FirstRow(string xml)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch { return map; }

        // The row is the first element that has only leaf (text) children and
        // is not part of the XSD schema. DocumentElement → row → columns.
        var docElement = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "DocumentElement");
        var rowElement = docElement?.Elements().FirstOrDefault()
            ?? doc.Descendants().FirstOrDefault(e =>
                e.HasElements
                && e.Elements().All(c => !c.HasElements)
                && !e.Name.LocalName.StartsWith("schema", StringComparison.OrdinalIgnoreCase));

        if (rowElement is null) return map;
        foreach (var col in rowElement.Elements())
            map[col.Name.LocalName] = col.Value;
        return map;
    }

    private static string? Pick(Dictionary<string, string> row, string[] keys)
    {
        foreach (var k in keys)
            if (row.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                return v.Trim();
        return null;
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
