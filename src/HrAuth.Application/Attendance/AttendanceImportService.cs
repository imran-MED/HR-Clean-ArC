using System.Globalization;
using HrAuth.Application.Attendance.Dtos;
using HrAuth.Application.Common;
using HrAuth.Domain.Attendance;

namespace HrAuth.Application.Attendance;

/// <summary>
/// Parses attendance machine export files into <see cref="AttendanceRecord"/>s,
/// applying the 8-digit employee-ID rule from the Domain layer.
/// </summary>
public sealed class AttendanceImportService : IAttendanceImportService
{
    private const int ExpectedColumns = 7;

    /// <summary>Accepted "Date Time" formats, tried in order.</summary>
    private static readonly string[] TimestampFormats =
    {
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd H:mm:ss",
        "yyyy/MM/dd HH:mm:ss",
        "dd/MM/yyyy HH:mm:ss",
    };

    public Result<AttendanceImportResult> Parse(string fileName, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Result<AttendanceImportResult>.Failure("The uploaded file is empty.");

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var records = new List<AttendanceRecord>();
        var errors = new List<string>();
        var totalLines = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var line = lines[i].Trim();
            if (line.Length == 0)
                continue; // ignore blank lines entirely

            totalLines++;

            // Columns are separated by any run of whitespace (spaces or tabs).
            var cols = line.Split(
                (char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (cols.Length < ExpectedColumns)
            {
                errors.Add($"Line {lineNumber}: expected {ExpectedColumns} columns, "
                           + $"found {cols.Length}.");
                continue;
            }

            var rawId = cols[0];
            var timestampText = $"{cols[1]} {cols[2]}";
            if (!TryParseTimestamp(timestampText, out var timestamp))
            {
                errors.Add($"Line {lineNumber}: unrecognized date/time "
                           + $"'{timestampText}'.");
                continue;
            }

            records.Add(new AttendanceRecord
            {
                LineNumber = lineNumber,
                RawEmployeeId = rawId,
                EmployeeId = EmployeeIdRules.Normalize(rawId), // → 8 digits
                Timestamp = timestamp,
                Status = ParseInt(cols[3]),
                Verify = ParseInt(cols[4]),
                WorkCode = ParseInt(cols[5]),
                Reserved = ParseInt(cols[6]),
            });
        }

        var dtos = records
            .Select(r => new AttendanceRecordDto(
                r.LineNumber, r.EmployeeId, r.RawEmployeeId, r.Timestamp,
                r.Status, r.Verify, r.WorkCode, r.Reserved))
            .ToList();

        return Result<AttendanceImportResult>.Success(new AttendanceImportResult(
            FileName: fileName,
            TotalLines: totalLines,
            ImportedCount: dtos.Count,
            SkippedCount: totalLines - dtos.Count,
            Records: dtos,
            Errors: errors));
    }

    private static bool TryParseTimestamp(string text, out DateTime value) =>
        DateTime.TryParseExact(text, TimestampFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out value)
        || DateTime.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out value);

    private static int ParseInt(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : 0;
}
