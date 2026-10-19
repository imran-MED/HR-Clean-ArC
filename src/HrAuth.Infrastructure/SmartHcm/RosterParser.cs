using System.Text.Json;
using System.Text.RegularExpressions;
using HrAuth.Domain.Rosters;

namespace HrAuth.Infrastructure.SmartHcm;

/// <summary>
/// Parses the JSON payload of <c>get_periodBreakup_date_emp_wise</c> into a
/// <see cref="Roster"/>. Expected shape:
/// <code>
/// {"success":true,"PeriodBreakup":{"Table":[
///   {"SHORT_NAME":"man8-b1","START_TIME":"05:00","END_TIME":"09:00",
///    "REQUIRED_HOURS":4.0,"SEQ_NO":537.0}, ... ]}}
/// </code>
/// When no shift is assigned the payload is <c>{"PeriodBreakup":{}}</c>.
/// </summary>
internal static partial class RosterParser
{
    public static Roster Parse(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!TryGetRows(document.RootElement, out var rows))
            return Roster.Empty;

        var periods = new List<RosterPeriod>();
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
                continue;

            var name = (Str(row, "SHORT_NAME") ?? Str(row, "PERIOD_NAME") ?? "").Trim();
            var start = (Str(row, "START_TIME") ?? Str(row, "IN_TIME") ?? "").Trim();
            var end = (Str(row, "END_TIME") ?? Str(row, "OUT_TIME") ?? "").Trim();
            if (name.Length == 0 && start.Length == 0 && end.Length == 0)
                continue;

            periods.Add(new RosterPeriod
            {
                Name = name,
                StartTime = start,
                EndTime = end,
                RequiredHours = Num(row, "REQUIRED_HOURS"),
                SequenceNo = (int)Num(row, "SEQ_NO"),
            });
        }

        if (periods.Count == 0)
            return Roster.Empty;

        // Order by sequence so the timeline reads start-to-end.
        periods.Sort((a, b) => a.SequenceNo.CompareTo(b.SequenceNo));

        return new Roster
        {
            ShiftName = DeriveShiftName(periods[0].Name),
            ShiftStart = periods[0].StartTime,
            ShiftEnd = periods[^1].EndTime,
            Periods = periods,
        };
    }

    /// <summary>Locate the row array, tolerating the known wrapper shapes.</summary>
    private static bool TryGetRows(JsonElement root, out JsonElement rows)
    {
        rows = default;

        if (root.ValueKind == JsonValueKind.Array)
        {
            rows = root;
            return true;
        }
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        if (root.TryGetProperty("PeriodBreakup", out var breakup) &&
            breakup.ValueKind == JsonValueKind.Object &&
            breakup.TryGetProperty("Table", out var table) &&
            table.ValueKind == JsonValueKind.Array)
        {
            rows = table;
            return true;
        }

        foreach (var key in new[] { "Table", "Records" })
        {
            if (root.TryGetProperty(key, out var arr) &&
                arr.ValueKind == JsonValueKind.Array)
            {
                rows = arr;
                return true;
            }
        }
        return false;
    }

    private static string? Str(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.ToString(),
                _ => null,
            }
            : null;

    private static double Num(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var v) &&
        v.ValueKind == JsonValueKind.Number &&
        v.TryGetDouble(out var d)
            ? d
            : 0d;

    /// <summary>
    /// Shift name = the first period name up to the first space or dash
    /// (e.g. "man8-b1" → "man8").
    /// </summary>
    private static string DeriveShiftName(string periodName)
    {
        var match = ShiftPrefix().Match(periodName);
        return match.Success ? match.Groups[1].Value : periodName;
    }

    [GeneratedRegex(@"^([^\s-]+)")]
    private static partial Regex ShiftPrefix();
}
