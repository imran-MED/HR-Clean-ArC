using System.Globalization;
using HrAuth.Application.Attendance;
using HrAuth.Application.Attendance.Dtos;
using HrAuth.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrAuth.Infrastructure.SmartHcm;

/// <summary>
/// <see cref="IAttendanceSummaryProvider"/> over the ASMX
/// <c>Attendance_GetAttendance</c> operation — invoked over HTTP GET with the
/// exact parameter shape the Dawam mobile app captured: <c>type=MAD</c>, empty
/// filter flags, dates as <c>"MM dd yyyy"</c>, password as base64(md5(pwd)),
/// static token key. Returns SmartHCM's computed per-day rows.
/// </summary>
public sealed class SmartHcmAttendanceSummaryProvider(
    SmartHcmClient client,
    IOptions<SmartHcmOptions> options,
    ILogger<SmartHcmAttendanceSummaryProvider> logger) : IAttendanceSummaryProvider
{
    private readonly SmartHcmOptions _options = options.Value;

    public async Task<Result<IReadOnlyList<AttendanceDaySummaryDto>>> GetAsync(
        string employeeId, string plaintextPassword,
        DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        string xml;
        try
        {
            xml = await client.CallGetAsync(
                "Attendance_GetAttendance",
                new Dictionary<string, string>
                {
                    ["employeeId"] = employeeId,
                    ["password"] = SmartHcmCrypto.HashPassword(plaintextPassword),
                    ["isAbsent"] = string.Empty,
                    ["isMissing"] = string.Empty,
                    ["empStatus"] = string.Empty,
                    ["type"] = "MAD",
                    ["dFrom"] = from.ToString("MM dd yyyy", CultureInfo.InvariantCulture),
                    ["dTo"] = to.ToString("MM dd yyyy", CultureInfo.InvariantCulture),
                    ["langCode"] = string.IsNullOrWhiteSpace(_options.LanguageCode)
                        ? "en-US" : _options.LanguageCode,
                    ["tokenKey"] = _options.TokenKey,
                },
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Attendance_GetAttendance failed for {EmployeeId}", employeeId);
            return Result<IReadOnlyList<AttendanceDaySummaryDto>>.Failure(
                $"Could not reach the SmartHCM attendance service: {ex.Message}");
        }

        var rows = SmartHcmClient.ParseDataTable(xml);

        // The server packages failures as a single-field tblError row.
        var error = rows.FirstOrDefault(r =>
            r.Count == 1 && (r.ContainsKey("Message") || r.ContainsKey("message")));
        if (error is not null)
        {
            var msg = (error.GetValueOrDefault("Message")
                       ?? error.GetValueOrDefault("message") ?? string.Empty).Trim();
            if (msg.Contains("Record_Not_Found", StringComparison.OrdinalIgnoreCase))
                return Result<IReadOnlyList<AttendanceDaySummaryDto>>.Success([]);
            return Result<IReadOnlyList<AttendanceDaySummaryDto>>.Failure(
                msg.Length > 0 ? msg : "The attendance service returned an error.");
        }

        var days = rows
            .Where(r => r.ContainsKey("ATTEND_DATE"))
            .Select(r => new AttendanceDaySummaryDto(
                Date: Get(r, "ATTEND_DATE") ?? string.Empty,
                DateHijri: Get(r, "ATTEND_DATE_HIJRI"),
                DayName: Get(r, "ATTEND_DATE_DAY"),
                TimeIn: Get(r, "TIME_IN"),
                TimeOut: Get(r, "TIME_OUT"),
                Shift: Get(r, "SHIFT"),
                Location: Get(r, "LOCATION"),
                Remarks: Get(r, "REMARKS"),
                RequiredHours: Get(r, "REQUIRED_HOURS"),
                PresentHours: Get(r, "PRESENT_HOURS"),
                IsMissing: string.Equals(Get(r, "IS_MISSING"), "Y",
                    StringComparison.OrdinalIgnoreCase)))
            .ToList();

        logger.LogInformation(
            "Attendance_GetAttendance for {EmployeeId} {From}..{To}: {Count} day(s).",
            employeeId, from, to, days.Count);

        return Result<IReadOnlyList<AttendanceDaySummaryDto>>.Success(days);
    }

    private static string? Get(IReadOnlyDictionary<string, string> row, string key)
    {
        var v = row.GetValueOrDefault(key)?.Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }
}
