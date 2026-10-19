using System.Text.Json;
using System.Text.RegularExpressions;
using HrAuth.Application.Leaves.Requests.Dtos;

namespace HrAuth.Application.Leaves.Requests;

/// <summary>
/// Business rules for leave requests, ported from the Dawam app's
/// <c>LeaveService</c> so the web app behaves the same way.
/// </summary>
public static partial class LeaveRequestRules
{
    /// <summary>Approve.</summary>
    public const string Approve = "A";
    /// <summary>Reject.</summary>
    public const string Reject = "R";
    /// <summary>Withdraw / cancel.</summary>
    public const string Withdraw = "C";

    /// <summary>
    /// Leave types (<c>LEAVE_TYPE</c>) that route through the supervisor to a
    /// further HR-level approval — his action never finalizes the request, so
    /// the approvals screen offers Forward (the same 'A' action) + Reject.
    /// List supplied by HR ops (mirrors Dawam's <c>hrApprovalLeaveTypeIds</c>).
    /// </summary>
    public static readonly IReadOnlySet<string> HrApprovalLeaveTypeIds =
        new HashSet<string> { "173", "175", "178", "179", "181", "182", "183", "184", "185" };

    public static bool RequiresHrApproval(string? leaveType) =>
        HrApprovalLeaveTypeIds.Contains((leaveType ?? string.Empty).Trim());

    /// <summary>
    /// True when the request is still awaiting a decision: <c>STATUS_ID</c>
    /// 'P' (or 'S' = submitted), falling back to the localized status text
    /// when the id column is missing.
    /// </summary>
    public static bool IsPending(string? statusId, string? status)
    {
        var id = (statusId ?? string.Empty).Trim().ToUpperInvariant();
        if (id.Length > 0) return id is "P" or "S";
        var s = (status ?? string.Empty).Trim();
        var lc = s.ToLowerInvariant();
        return lc.Contains("pending") || lc.Contains("submit")
            || s.Contains("معلق") || s.Contains("انتظار");
    }

    /// <summary>
    /// Employee-id equality tolerating leading zeros (<c>00000314</c> = <c>314</c>).
    /// </summary>
    public static bool IdsMatch(string? a, string? b)
    {
        a = (a ?? string.Empty).Trim();
        b = (b ?? string.Empty).Trim();
        if (a.Length == 0 || b.Length == 0) return false;
        if (a == b) return true;
        return long.TryParse(a, out var ai) && long.TryParse(b, out var bi) && ai == bi;
    }

    /// <summary>
    /// Normalize a SmartHCM write reply (<c>SaveLeave</c>, <c>PerformAction</c>).
    /// Two shapes: a JSON envelope <c>{"success":false,"message":"…"}</c> whose
    /// flag is authoritative (business-rule rejections arrive with a localized
    /// message and no English keywords), or a legacy plain string optionally
    /// prefixed <c>1:1000000123:</c>, judged by keywords. Leaked .NET exception
    /// text is replaced so it never reaches the user.
    /// </summary>
    public static LeaveWriteResultDto ParseWriteResponse(string body)
    {
        var trimmed = (body ?? string.Empty).Trim();

        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var root = doc.RootElement;
                    var ok = false;
                    if (TryProp(root, out var flag, "success", "Success"))
                        ok = flag.ValueKind == JsonValueKind.True
                             || (flag.ValueKind == JsonValueKind.String
                                 && string.Equals(flag.GetString(), "true",
                                     StringComparison.OrdinalIgnoreCase));
                    var message = TryProp(root, out var m, "message", "Message", "msg")
                        ? (m.ValueKind == JsonValueKind.String ? m.GetString() : m.ToString()) ?? string.Empty
                        : string.Empty;
                    return new LeaveWriteResultDto(ok, message.Trim());
                }
            }
            catch (JsonException)
            {
                // Not JSON after all — fall through to the plain-string rules.
            }
        }

        var clean = CodePrefix().Replace(trimmed, string.Empty).Trim();
        var lc = clean.ToLowerInvariant();
        var isServerException = lc.Contains("object reference")
            || lc.Contains("nullreferenceexception")
            || lc.Contains("at system.")
            || lc.Contains("system.web.")
            || lc.Contains("exception of type");
        var isError = isServerException
            || lc.Contains("error") || lc.Contains("fail") || lc.Contains("invalid");

        return new LeaveWriteResultDto(
            !isError,
            isServerException
                ? "SmartHCM returned an internal error. Please try again or contact HR."
                : clean);
    }

    private static bool TryProp(JsonElement obj, out JsonElement value, params string[] names)
    {
        foreach (var n in names)
            if (obj.TryGetProperty(n, out value)) return true;
        value = default;
        return false;
    }

    [GeneratedRegex(@"^\d+:\d+:")]
    private static partial Regex CodePrefix();
}
