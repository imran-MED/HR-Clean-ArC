using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HrAuth.Application.Abstractions;
using HrAuth.Application.Common;
using HrAuth.Application.Leaves.Requests;
using HrAuth.Application.Leaves.Requests.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrAuth.Infrastructure.SmartHcm;

/// <summary>
/// <see cref="ILeaveRequestProvider"/> over the SmartHCM ASMX service — a port
/// of the Dawam app's <c>LeaveService</c> (lib/core/services/leave_service.dart):
/// same operations, same parameter names, same date formats, same HTTP verbs.
/// Every call carries <c>TokenKey</c> and the caller's hashed <c>Password</c>.
/// </summary>
public sealed partial class SmartHcmLeaveRequestProvider(
    SmartHcmClient client,
    IOptions<SmartHcmOptions> options,
    ILogger<SmartHcmLeaveRequestProvider> logger) : ILeaveRequestProvider
{
    private readonly SmartHcmOptions _options = options.Value;

    /// <summary>
    /// <c>Hrs_GetPendingLeaves</c> returns a supervisor's whole history
    /// newest-first; at small page sizes resolved rows push pending ones off
    /// the first page. 500 covers it in one request (same as Dawam).
    /// </summary>
    private const int ListPageSize = 500;

    // SaveLeave wants MM-dd-yyyy; GET_TO_DATE wants yyyy-MM-dd (server quirk).
    private const string SaveLeaveDateFormat = "MM-dd-yyyy";
    private const string ToDateDateFormat = "yyyy-MM-dd";

    public async Task<Result<IReadOnlyList<LeaveTypeDto>>> GetLeaveTypesAsync(
        SmartHcmCredential caller, CancellationToken ct = default)
    {
        try
        {
            var xml = await client.CallAsync("GetLeaveTypes", WithAuth(caller, new()
            {
                ["p_emp_id"] = caller.EmployeeId,
                ["userlanguage"] = _options.LanguageCode,
            }), ct);

            var inner = SmartHcmClient.ParseStringResponse(xml).Trim();
            var types = new List<LeaveTypeDto>();
            if (inner.StartsWith('{'))
            {
                using var doc = JsonDocument.Parse(inner);
                if (doc.RootElement.TryGetProperty("Records", out var records)
                    && records.ValueKind == JsonValueKind.Array)
                {
                    foreach (var rec in records.EnumerateArray())
                    {
                        if (rec.ValueKind != JsonValueKind.Object) continue;
                        var f = ToStringMap(rec);
                        var id = Get(f, "LEAVE_TYPE");
                        if (id.Length == 0) continue;
                        types.Add(new LeaveTypeDto(
                            Id: id,
                            Name: FirstNonEmpty(Get(f, "TYPE_DESC"), Get(f, "SHORT_NAME"), id),
                            ShortName: NullIfEmpty(Get(f, "SHORT_NAME")),
                            MaxStretch: NullIfEmpty(Get(f, "MAX_STRETCH")),
                            MinRequiredLimit: NullIfEmpty(Get(f, "MIN_REQUIRED_LIMIT")),
                            Fields: f));
                    }
                }
            }

            if (types.Count == 0)
                logger.LogWarning("GetLeaveTypes returned no Records for {Emp}: {Body}",
                    caller.EmployeeId, Truncate(inner));
            return Result<IReadOnlyList<LeaveTypeDto>>.Success(types);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetLeaveTypes failed for {Emp}", caller.EmployeeId);
            return Result<IReadOnlyList<LeaveTypeDto>>.Failure(Unreachable(ex));
        }
    }

    public async Task<Result<LeaveDaysDto>> ComputeDaysAsync(
        SmartHcmCredential caller, string leaveTypeId, DateOnly from, DateOnly to,
        string? fromTime, string? toTime, CancellationToken ct = default)
    {
        try
        {
            // Note the field names: GET_TO_DATE uses p_from_time / p_to_time,
            // SaveLeave the reversed p_time_from / p_time_to. Intentional server-side.
            var xml = await client.CallAsync("GET_TO_DATE", WithAuth(caller, new()
            {
                ["p_emp_id"] = caller.EmployeeId,
                ["p_date_from"] = from.ToString(ToDateDateFormat, CultureInfo.InvariantCulture),
                ["p_date_to"] = to.ToString(ToDateDateFormat, CultureInfo.InvariantCulture),
                ["p_no_of_days"] = string.Empty,
                ["p_leave_type"] = leaveTypeId,
                ["p_from_time"] = fromTime ?? string.Empty,
                ["p_to_time"] = toTime ?? string.Empty,
            }), ct);

            var body = SmartHcmClient.ParseStringResponse(xml).Trim();
            var m = FirstNumber().Match(body);
            double? days = m.Success && double.TryParse(m.Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var d) ? d : null;
            if (days is null)
                logger.LogWarning("GET_TO_DATE gave no number for {Emp}: {Body}",
                    caller.EmployeeId, Truncate(body));
            return Result<LeaveDaysDto>.Success(new LeaveDaysDto(body, days));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GET_TO_DATE failed for {Emp}", caller.EmployeeId);
            return Result<LeaveDaysDto>.Failure(Unreachable(ex));
        }
    }

    public async Task<Result<SupervisorDto>> GetSupervisorAsync(
        SmartHcmCredential caller, string leaveTypeId, double days, string? rflNo,
        CancellationToken ct = default)
    {
        try
        {
            var xml = await client.CallAsync("Leave_getSupervisorName", WithAuth(caller, new()
            {
                ["p_rflno"] = rflNo ?? string.Empty,
                ["p_applicant"] = caller.EmployeeId,
                ["p_leave_type"] = leaveTypeId,
                ["p_appr_days"] = days.ToString("0.##", CultureInfo.InvariantCulture),
                ["p_emp_id"] = caller.EmployeeId,
            }), ct);

            var rows = SmartHcmClient.ParseDataTable(xml);
            var row = rows.FirstOrDefault(r => !IsErrorRow(r));
            if (row is null)
                return Result<SupervisorDto>.Success(new SupervisorDto(string.Empty, string.Empty));

            var id = Pick(row, "SupervisorId", "SUPERVISOR_ID", "APPROVER_ID", "EMP_ID",
                "EmployeeID", "EmployeeId", "Id");
            var name = Pick(row, "SupervisorName", "SUPERVISOR_NAME", "EMP_NAME", "Name", "NAME",
                "FullName", "Description", "EMP_FIRST_NAME");
            if (name.Length == 0)
                name = row.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v) && v != id)?.Trim()
                       ?? string.Empty;
            return Result<SupervisorDto>.Success(new SupervisorDto(id, name));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Leave_getSupervisorName failed for {Emp}", caller.EmployeeId);
            return Result<SupervisorDto>.Failure(Unreachable(ex));
        }
    }

    public async Task<Result<LeaveWriteResultDto>> SubmitAsync(
        SmartHcmCredential caller, ApplyLeaveCommand command, CancellationToken ct = default)
    {
        try
        {
            // Real SaveLeave contract (captured by the Dawam team from a working
            // client). p_medical_cert MUST be 'Y' when files are attached — it
            // becomes MEDICAL_CERT_ATT, which is what approvers see.
            var parameters = WithAuth(caller, new()
            {
                ["p_emp_id"] = caller.EmployeeId,
                ["p_date_from"] = command.FromDate.ToString(SaveLeaveDateFormat, CultureInfo.InvariantCulture),
                ["p_date_to"] = command.ToDate.ToString(SaveLeaveDateFormat, CultureInfo.InvariantCulture),
                ["p_time_from"] = command.FromTime ?? string.Empty,
                ["p_time_to"] = command.ToTime ?? string.Empty,
                ["p_no_of_days"] = command.NoOfDays.ToString("0.##", CultureInfo.InvariantCulture),
                ["p_leave_type"] = command.LeaveTypeId,
                ["p_advance_salary"] = "N",
                ["p_medical_cert"] = command.Attachments.Count > 0 ? "Y" : "N",
                ["p_remarks"] = command.Remarks ?? string.Empty,
                ["p_user_lang"] = _options.LanguageCode,
                ["p_alternate_emp"] = string.Empty,
            });

            // Attachments must ride in the SAME call: the server only knows the
            // new request number inside SaveLeave and attaches Request.Files
            // there. (Leave_attachement is read-only and discards uploads.)
            var xml = command.Attachments.Count == 0
                ? await client.CallAsync("SaveLeave", parameters, ct)
                : await client.CallMultipartAsync("SaveLeave", parameters,
                    command.Attachments.Select(a => (a.FileName, a.ContentType, a.Content)).ToList(), ct);

            var body = SmartHcmClient.ParseStringResponse(xml).Trim();
            var result = LeaveRequestRules.ParseWriteResponse(body);
            logger.LogInformation(
                "SaveLeave for {Emp} type {Type} {From}..{To} ({Files} file(s)): success={Ok} {Body}",
                caller.EmployeeId, command.LeaveTypeId, command.FromDate, command.ToDate,
                command.Attachments.Count, result.Success, Truncate(body));
            return Result<LeaveWriteResultDto>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveLeave failed for {Emp}", caller.EmployeeId);
            return Result<LeaveWriteResultDto>.Failure(Unreachable(ex));
        }
    }

    public async Task<Result<IReadOnlyList<LeaveRequestRowDto>>> ListVisibleAsync(
        SmartHcmCredential caller, CancellationToken ct = default)
    {
        try
        {
            // p_status / p_actorType are accepted but IGNORED by the server
            // (probed by the Dawam team) — filtering happens on our side.
            var xml = await client.CallGetAsync("Hrs_GetPendingLeaves", WithAuth(caller, new()
            {
                ["p_emp_id"] = caller.EmployeeId,
                ["p_status"] = string.Empty,
                ["p_actorType"] = string.Empty,
                ["langCode"] = _options.LanguageCode,
                ["SelectedLeave"] = string.Empty,
                ["p_pageNumber"] = "1",
                ["p_pageSize"] = ListPageSize.ToString(CultureInfo.InvariantCulture),
            }), ct);

            var rows = SmartHcmClient.ParseDataTable(xml);
            var error = rows.FirstOrDefault(IsErrorRow);
            if (error is not null && rows.All(IsErrorRow))
            {
                var message = error.Values.FirstOrDefault() ?? "unknown error";
                logger.LogWarning("Hrs_GetPendingLeaves error for {Emp}: {Msg}", caller.EmployeeId, message);
                return Result<IReadOnlyList<LeaveRequestRowDto>>.Failure(
                    $"SmartHCM rejected the request: {message}");
            }

            var mapped = rows.Where(r => !IsErrorRow(r)).Select(r => ToRow(r, caller)).ToList();
            if (mapped.Count > 0 && mapped.All(r => r.RflNo is null))
                logger.LogWarning(
                    "Hrs_GetPendingLeaves rows carry no recognised request-number column; columns: {Cols}",
                    string.Join(", ", rows.First(r => !IsErrorRow(r)).Keys));

            return Result<IReadOnlyList<LeaveRequestRowDto>>.Success(mapped);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Hrs_GetPendingLeaves failed for {Emp}", caller.EmployeeId);
            return Result<IReadOnlyList<LeaveRequestRowDto>>.Failure(Unreachable(ex));
        }
    }

    public async Task<Result<IReadOnlyDictionary<string, string>>> GetDetailAsync(
        SmartHcmCredential caller, string rflNo, string? applicantId,
        CancellationToken ct = default)
    {
        try
        {
            var row = await FetchDetail(caller, caller.EmployeeId, rflNo, ct);
            // Some SmartHCM builds key the lookup on the applicant instead.
            if (row is null && !string.IsNullOrWhiteSpace(applicantId)
                && !LeaveRequestRules.IdsMatch(applicantId, caller.EmployeeId))
                row = await FetchDetail(caller, applicantId.Trim(), rflNo, ct);

            return Result<IReadOnlyDictionary<string, string>>.Success(
                row ?? new Dictionary<string, string>());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Hrs_GetLeaveDetail failed for {Rfl}", rflNo);
            return Result<IReadOnlyDictionary<string, string>>.Failure(Unreachable(ex));
        }
    }

    public async Task<Result<LeaveWriteResultDto>> PerformActionAsync(
        SmartHcmCredential caller, string rflNo, string action, string? remarks,
        string approvedDays, CancellationToken ct = default)
    {
        try
        {
            // Captured from the original SmartHCM Mobile (GET):
            // Hrs_Leave_PerformAction?p_rflno=…&p_actor_id=…&p_action=A&p_remarks=
            //   &p_approved_days=1&p_approver_id=…&p_leave_type_flag=&p_last_level=N
            //   &assignAuthority=&Password=…&TokenKey=…&language=en-US
            var xml = await client.CallGetAsync("Hrs_Leave_PerformAction", WithAuth(caller, new()
            {
                ["p_rflno"] = rflNo,
                ["p_actor_id"] = caller.EmployeeId,
                ["p_action"] = action,
                ["p_remarks"] = remarks ?? string.Empty,
                ["p_approved_days"] = approvedDays,
                ["p_approver_id"] = caller.EmployeeId,
                ["p_leave_type_flag"] = string.Empty,
                ["p_last_level"] = "N",
                ["assignAuthority"] = string.Empty,
                ["language"] = _options.LanguageCode,
            }), ct);

            var body = SmartHcmClient.ParseStringResponse(xml).Trim();
            var result = LeaveRequestRules.ParseWriteResponse(body);
            logger.LogInformation(
                "Hrs_Leave_PerformAction {Action} on {Rfl} by {Emp}: success={Ok} {Body}",
                action, rflNo, caller.EmployeeId, result.Success, Truncate(body));
            return Result<LeaveWriteResultDto>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Hrs_Leave_PerformAction failed for {Rfl}", rflNo);
            return Result<LeaveWriteResultDto>.Failure(Unreachable(ex));
        }
    }

    // ------------------------------------------------------------------ helpers

    private async Task<IReadOnlyDictionary<string, string>?> FetchDetail(
        SmartHcmCredential caller, string empId, string rflNo, CancellationToken ct)
    {
        var xml = await client.CallAsync("Hrs_GetLeaveDetail", WithAuth(caller, new()
        {
            ["p_rflno"] = rflNo,
            ["p_emp_id"] = empId,
            ["P_LANG"] = _options.LanguageCode,
        }), ct);
        return SmartHcmClient.ParseDataTable(xml).FirstOrDefault(r => !IsErrorRow(r));
    }

    /// <summary>Add the TokenKey + caller's hashed password every call carries.</summary>
    private Dictionary<string, string> WithAuth(
        SmartHcmCredential caller, Dictionary<string, string> parameters)
    {
        parameters["Password"] = caller.PasswordHash;
        parameters["TokenKey"] = _options.TokenKey;
        return parameters;
    }

    private static LeaveRequestRowDto ToRow(IReadOnlyDictionary<string, string> r, SmartHcmCredential caller)
    {
        var empId = Get(r, "EMPLOYEE_ID");
        var statusId = NullIfEmpty(Get(r, "STATUS_ID"));
        var status = NullIfEmpty(Get(r, "STATUS"));
        var leaveType = NullIfEmpty(Get(r, "LEAVE_TYPE"));
        double? days = double.TryParse(Get(r, "DAY_HOUR_REQUESTED"), NumberStyles.Any,
            CultureInfo.InvariantCulture, out var d) ? d : null;

        return new LeaveRequestRowDto(
            RflNo: FindRflNo(r),
            EmployeeId: empId,
            EmployeeName: NullIfEmpty(Get(r, "EMPLOYEE_NAME")),
            LeaveType: leaveType,
            LeaveName: NullIfEmpty(Get(r, "LEAVE_NAME")),
            FromDate: SmartHcmLeaveProvider.ParseLeaveDate(Get(r, "DATE_FROM")),
            ToDate: SmartHcmLeaveProvider.ParseLeaveDate(Get(r, "DATE_TO")),
            Status: status,
            StatusId: statusId,
            DayHourRequested: days,
            DayType: NullIfEmpty(Get(r, "LEAVE_DAY_TYPE")),
            IsPending: LeaveRequestRules.IsPending(statusId, status),
            IsOwn: LeaveRequestRules.IdsMatch(empId, caller.EmployeeId),
            RequiresHrApproval: LeaveRequestRules.RequiresHrApproval(leaveType),
            Fields: r);
    }

    /// <summary>
    /// The request number column. Dawam reads it from the list row; the exact
    /// column name varies by SmartHCM build, so try the known spellings, then
    /// any column whose name contains "RFL".
    /// </summary>
    private static string? FindRflNo(IReadOnlyDictionary<string, string> r)
    {
        var known = Pick(r, "RFL_NO", "RFLNO", "P_RFLNO", "RFL", "LEAVE_RFL_NO", "REQUEST_NO", "LEAVE_RECORD_ID");
        if (known.Length > 0) return known;
        var any = r.FirstOrDefault(kv =>
            kv.Key.Contains("RFL", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(kv.Value));
        return string.IsNullOrWhiteSpace(any.Value) ? null : any.Value.Trim();
    }

    private static bool IsErrorRow(IReadOnlyDictionary<string, string> r) =>
        r.Keys.Any(k => k.Contains("error", StringComparison.OrdinalIgnoreCase));

    private static Dictionary<string, string> ToStringMap(JsonElement obj)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in obj.EnumerateObject())
        {
            var v = p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString(),
                JsonValueKind.Number => p.Value.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null,
            };
            if (v is not null) map[p.Name] = v;
        }
        return map;
    }

    private static string Pick(IReadOnlyDictionary<string, string> r, params string[] keys)
    {
        foreach (var k in keys)
            if (r.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                return v.Trim();
        return string.Empty;
    }

    private static string Get(IReadOnlyDictionary<string, string> r, string key)
        => r.TryGetValue(key, out var v) ? (v ?? string.Empty).Trim() : string.Empty;

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;

    private static string Truncate(string s) => s.Length > 400 ? s[..400] + "…" : s;

    private static string Unreachable(Exception ex) => $"Could not reach the leave service: {ex.Message}";

    [GeneratedRegex(@"-?\d+(?:\.\d+)?")]
    private static partial Regex FirstNumber();
}
