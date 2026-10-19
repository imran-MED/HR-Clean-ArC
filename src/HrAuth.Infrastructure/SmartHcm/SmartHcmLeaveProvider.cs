using System.Globalization;
using System.Text.RegularExpressions;
using HrAuth.Application.Common;
using HrAuth.Application.Leaves;
using HrAuth.Application.Leaves.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrAuth.Infrastructure.SmartHcm;

/// <summary>
/// <see cref="ILeaveProvider"/> backed by the SmartHCM ASMX operation
/// <c>Hrs_GetPendingLeaves</c> (HTTP GET) — the same call the Dawam attendance
/// app uses. Returns every leave the caller can see; we keep the rows for the
/// requested employee. Requires that employee's own password (hashed).
/// </summary>
public sealed partial class SmartHcmLeaveProvider(
    SmartHcmClient client,
    IOptions<SmartHcmOptions> options,
    IMemoryCache cache,
    ILogger<SmartHcmLeaveProvider> logger) : ILeaveProvider
{
    private readonly SmartHcmOptions _options = options.Value;

    private const int MaxPages = 100;

    public async Task<Result<IReadOnlyList<LeaveDto>>> GetLeavesAsync(
        string employeeId, string password, CancellationToken ct = default)
    {
        try
        {
            var xml = await client.CallGetAsync(
                "Hrs_GetPendingLeaves",
                new Dictionary<string, string>
                {
                    ["p_emp_id"] = employeeId,
                    ["Password"] = SmartHcmCrypto.HashPassword(password ?? string.Empty),
                    ["p_status"] = string.Empty,
                    ["p_actorType"] = string.Empty,
                    ["langCode"] = _options.LanguageCode,
                    ["SelectedLeave"] = string.Empty,
                    ["p_pageNumber"] = "1",
                    ["p_pageSize"] = "200",
                    ["TokenKey"] = _options.TokenKey,
                },
                ct);

            var rows = SmartHcmClient.ParseDataTable(xml);

            // Auth/error payloads surface as a row carrying an "error" column.
            var errorRow = rows.FirstOrDefault(
                r => r.Keys.Any(k => k.Contains("error", StringComparison.OrdinalIgnoreCase)));
            if (errorRow is not null)
            {
                var message = errorRow.Values.FirstOrDefault() ?? "unknown error";
                logger.LogWarning("Hrs_GetPendingLeaves error for {Emp}: {Msg}",
                    employeeId, message);
                return Result<IReadOnlyList<LeaveDto>>.Failure(
                    $"SmartHCM rejected the leave request: {message}");
            }

            var leaves = rows
                .Where(r => IdsMatch(Get(r, "EMPLOYEE_ID"), employeeId))
                .Select(ToDto)
                .Where(l => l is not null)
                .Select(l => l!)
                .ToList();

            logger.LogInformation(
                "Fetched {Count} leave(s) for {Emp} from SmartHCM.", leaves.Count, employeeId);
            return Result<IReadOnlyList<LeaveDto>>.Success(leaves);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Leave fetch failed for {Emp}", employeeId);
            return Result<IReadOnlyList<LeaveDto>>.Failure(
                $"Could not reach the leave service: {ex.Message}");
        }
    }

    public async Task<Result<IReadOnlyList<LeaveDto>>> GetAllLeavesAsync(
        CancellationToken ct = default)
    {
        var adminId = _options.DirectoryAdminEmpId;
        var adminPw = _options.DirectoryAdminPassword;
        if (string.IsNullOrWhiteSpace(adminId) || string.IsNullOrWhiteSpace(adminPw))
            return Result<IReadOnlyList<LeaveDto>>.Failure(
                "No SmartHCM admin credential configured "
                + "(SmartHcm:DirectoryAdminEmpId / DirectoryAdminPassword).");

        var cacheKey = $"leaves:all:{DateTime.UtcNow:yyyy-MM-dd}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<LeaveDto>? cached) && cached is not null)
            return Result<IReadOnlyList<LeaveDto>>.Success(cached);

        try
        {
            var hashed = SmartHcmCrypto.HashPassword(adminPw);
            const int pageSize = 500;
            var all = new List<LeaveDto>();

            for (var page = 1; page <= MaxPages; page++)
            {
                var xml = await client.CallGetAsync(
                    "Hrs_GetPendingLeaves",
                    new Dictionary<string, string>
                    {
                        ["p_emp_id"] = adminId,
                        ["Password"] = hashed,
                        ["p_status"] = string.Empty,
                        ["p_actorType"] = string.Empty,
                        ["langCode"] = _options.LanguageCode,
                        ["SelectedLeave"] = string.Empty,
                        ["p_pageNumber"] = page.ToString(),
                        ["p_pageSize"] = pageSize.ToString(),
                        ["TokenKey"] = _options.TokenKey,
                    },
                    ct);

                var rows = SmartHcmClient.ParseDataTable(xml);
                var errorRow = rows.FirstOrDefault(
                    r => r.Keys.Any(k => k.Contains("error", StringComparison.OrdinalIgnoreCase)));
                if (errorRow is not null)
                {
                    if (page == 1)
                    {
                        var message = errorRow.Values.FirstOrDefault() ?? "unknown error";
                        logger.LogWarning("Admin Hrs_GetPendingLeaves error: {Msg}", message);
                        return Result<IReadOnlyList<LeaveDto>>.Failure(
                            $"SmartHCM rejected the admin leave request: {message}");
                    }
                    break; // an error on a later page just ends pagination
                }

                all.AddRange(rows.Select(ToDto).Where(l => l is not null).Select(l => l!));
                if (rows.Count < pageSize) break; // last page reached
            }

            IReadOnlyList<LeaveDto> result = all;
            cache.Set(cacheKey, result, TimeSpan.FromHours(12));
            logger.LogInformation("Fetched {Count} leave(s) via admin from SmartHCM.", all.Count);
            return Result<IReadOnlyList<LeaveDto>>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Admin leave fetch failed.");
            return Result<IReadOnlyList<LeaveDto>>.Failure(
                $"Could not reach the leave service: {ex.Message}");
        }
    }

    private static LeaveDto? ToDto(IReadOnlyDictionary<string, string> r)
    {
        var from = ParseLeaveDate(Get(r, "DATE_FROM"));
        var to = ParseLeaveDate(Get(r, "DATE_TO"));
        if (from is null)
            return null; // a leave with no readable start is unusable
        to ??= from;

        double? days = double.TryParse(
            Get(r, "DAY_HOUR_REQUESTED"), NumberStyles.Any,
            CultureInfo.InvariantCulture, out var d) ? d : null;

        return new LeaveDto(
            EmployeeId: Get(r, "EMPLOYEE_ID").Trim(),
            EmployeeName: NullIfEmpty(Get(r, "EMPLOYEE_NAME")),
            LeaveType: NullIfEmpty(Get(r, "LEAVE_TYPE")),
            LeaveName: NullIfEmpty(Get(r, "LEAVE_NAME")),
            FromDate: from.Value,
            ToDate: to.Value,
            Status: NullIfEmpty(Get(r, "STATUS")),
            StatusId: NullIfEmpty(Get(r, "STATUS_ID")),
            DayHourRequested: days,
            DayType: NullIfEmpty(Get(r, "LEAVE_DAY_TYPE")));
    }

    /// <summary>
    /// Normalize a SmartHCM leave date to <see cref="DateOnly"/>. Dates arrive
    /// in Oracle's NLS format (DD-MM-YYYY in Saudi configs, sometimes ISO).
    /// </summary>
    internal static DateOnly? ParseLeaveDate(string? raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (s.Length == 0) return null;
        var dateOnly = s.Split(['T', ' '], 2)[0];

        var iso = IsoDate().Match(dateOnly);
        if (iso.Success)
            return TryDate(int.Parse(iso.Groups[1].Value),
                int.Parse(iso.Groups[2].Value), int.Parse(iso.Groups[3].Value));

        var dmy = DmyDate().Match(dateOnly);
        if (dmy.Success)
        {
            var first = int.Parse(dmy.Groups[1].Value);
            var second = int.Parse(dmy.Groups[2].Value);
            var year = int.Parse(dmy.Groups[3].Value);
            // Default to DD-MM (Saudi/EU) unless the first group can only be a day.
            var monthFirst = first <= 12 && second > 12;
            var day = monthFirst ? second : first;
            var month = monthFirst ? first : second;
            return TryDate(year, month, day);
        }

        return null;
    }

    private static DateOnly? TryDate(int y, int m, int d)
    {
        try { return new DateOnly(y, m, d); }
        catch { return null; }
    }

    private static string Get(IReadOnlyDictionary<string, string> r, string key)
        => r.TryGetValue(key, out var v) ? v ?? string.Empty : string.Empty;

    private static string? NullIfEmpty(string s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool IdsMatch(string a, string b)
    {
        a = a.Trim(); b = b.Trim();
        if (a.Length == 0 || b.Length == 0) return false;
        if (a == b) return true;
        return int.TryParse(a, out var ai) && int.TryParse(b, out var bi) && ai == bi;
    }

    [GeneratedRegex(@"^(\d{4})[-/](\d{1,2})[-/](\d{1,2})$")]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"^(\d{1,2})[-/](\d{1,2})[-/](\d{4})$")]
    private static partial Regex DmyDate();
}
