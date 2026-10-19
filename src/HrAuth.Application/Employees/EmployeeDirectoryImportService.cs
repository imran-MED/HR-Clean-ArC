using System.Text.RegularExpressions;
using HrAuth.Application.Common;
using HrAuth.Application.Employees.Dtos;
using HrAuth.Application.Persistence;
using HrAuth.Domain.Attendance;

namespace HrAuth.Application.Employees;

/// <summary>
/// Default <see cref="IEmployeeDirectoryImportService"/>. Accepts CSV or TSV
/// content. Two layouts are detected automatically per row:
/// <list type="bullet">
///   <item><c>EmployeeId,Name</c> — names-only directory.</item>
///   <item><c>EmployeeId,Name,Position,SupervisorName</c> — the SmartHCM
///   "Employee Data Report" export. The 4th column holds the supervisor's
///   <b>name</b> (not an ID), which is resolved to an employee ID against the
///   file's own name column.</item>
/// </list>
/// Employee IDs are normalized to 8 digits before upserting.
/// </summary>
public sealed partial class EmployeeDirectoryImportService(
    IEmployeeRepository employees) : IEmployeeDirectoryImportService
{
    private const int MaxWarnings = 25;

    private sealed record Parsed(
        string Id, string Name, string? Position, string? SupervisorName);

    public async Task<Result<EmployeeDirectoryImportResult>> ImportAsync(
        string fileName, string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Result<EmployeeDirectoryImportResult>.Failure("The file is empty.");

        // Pass 1 — parse rows (last write wins for a repeated ID).
        var byId = new Dictionary<string, Parsed>(StringComparer.Ordinal);
        var warnings = new List<string>();
        var parsed = 0;
        var skipped = 0;
        var lineNo = 0;

        using var reader = new StringReader(content);
        string? raw;
        while ((raw = await reader.ReadLineAsync(ct)) is not null)
        {
            lineNo++;
            var line = lineNo == 1 ? raw.TrimStart('﻿').Trim() : raw.Trim();
            if (line.Length == 0) continue;

            if (lineNo == 1 && LooksLikeHeader(line))
                continue; // header row — not a data row

            parsed++;
            var (rec, error) = ParseRow(line);
            if (error is not null)
            {
                skipped++;
                if (warnings.Count < MaxWarnings)
                    warnings.Add($"Line {lineNo}: {error}");
                continue;
            }

            var normalized = EmployeeIdRules.Normalize(rec!.Id);
            byId[normalized] = rec with { Id = normalized };
        }

        // Build a name → employee-ID map so the supervisor *name* column can be
        // resolved to an ID. First occurrence wins on duplicate names.
        var nameToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in byId.Values)
        {
            var key = NormalizeName(r.Name);
            if (key.Length > 0)
                nameToId.TryAdd(key, r.Id);
        }

        // Pass 2 — resolve supervisors and assemble the upsert rows.
        var rows = new List<(string, string, string?, string?)>(byId.Count);
        var unresolved = 0;
        foreach (var r in byId.Values)
        {
            string? supervisorId = null;
            if (!string.IsNullOrWhiteSpace(r.SupervisorName))
            {
                if (nameToId.TryGetValue(NormalizeName(r.SupervisorName), out var sid)
                    && sid != r.Id) // never let an employee be their own supervisor
                {
                    supervisorId = sid;
                }
                else
                {
                    unresolved++;
                    if (warnings.Count < MaxWarnings)
                        warnings.Add(
                            $"{r.Id}: supervisor \"{r.SupervisorName.Trim()}\" not found in the file.");
                }
            }
            rows.Add((r.Id, r.Name, r.Position, supervisorId));
        }

        var imported = rows.Count > 0
            ? await employees.BulkUpsertDirectoryAsync(rows, ct)
            : 0;

        if (unresolved > 0)
            warnings.Add($"{unresolved} supervisor name(s) could not be matched to an employee ID.");

        var total = await employees.CountAsync(ct);

        return Result<EmployeeDirectoryImportResult>.Success(
            new EmployeeDirectoryImportResult(
                FileName: fileName,
                LinesParsed: parsed,
                RowsImported: imported,
                RowsSkipped: skipped,
                TotalEmployeesInDb: total,
                Warnings: warnings));
    }

    /// <summary>
    /// True when the line looks like a header — the first column contains no
    /// digits at all (e.g. "EmployeeId" or the Arabic "رقم الموظف").
    /// </summary>
    private static bool LooksLikeHeader(string line)
    {
        var cut = line.IndexOfAny(['\t', ';', ',']);
        var first = (cut < 0 ? line : line[..cut]).Trim().Trim('"');
        return first.Length > 0 && !first.Any(char.IsDigit);
    }

    /// <summary>
    /// Parse a CSV/TSV row into (id, name, position?, supervisorName?). Comma,
    /// tab, and semicolon are accepted as separators; quoted fields are
    /// supported. Extra columns beyond the 4th are ignored.
    /// </summary>
    private static (Parsed? Rec, string? Error) ParseRow(string line)
    {
        var sep = line.Contains('\t') ? '\t'
                : (line.Contains(';') && !line.Contains(',')) ? ';'
                : ',';

        var fields = SplitCsv(line, sep);
        if (fields.Count < 2)
            return (null, "expected at least ID and Name");

        var id = fields[0].Trim().Trim('"');
        var name = fields[1].Trim().Trim('"');
        if (id.Length == 0) return (null, "empty employee ID");
        if (name.Length == 0) return (null, "empty name");
        if (!id.All(char.IsDigit)) return (null, $"non-numeric ID '{id}'");

        var position = fields.Count > 2 ? Clean(fields[2]) : null;
        var supervisor = fields.Count > 3 ? Clean(fields[3]) : null;
        return (new Parsed(id, name, position, supervisor), null);
    }

    /// <summary>Split one CSV line on <paramref name="sep"/>, honoring quotes.</summary>
    private static List<string> SplitCsv(string line, char sep)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                { current.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (c == sep && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else current.Append(c);
        }
        fields.Add(current.ToString());
        return fields;
    }

    private static string? Clean(string field)
    {
        var v = field.Trim().Trim('"').Trim();
        return v.Length == 0 ? null : v;
    }

    /// <summary>
    /// Normalize an Arabic name for matching: drop tashkeel diacritics and
    /// tatweel, unify alef/ya/ta-marbuta variants, then collapse whitespace.
    /// This lets a supervisor name resolve to an employee even when the two
    /// spellings differ only by diacritics (the cause of the few misses in the
    /// SmartHCM export).
    /// </summary>
    private static string NormalizeName(string name)
    {
        // Remove tashkeel (ً-ْ), superscript alef (ٰ), tatweel (ـ).
        var stripped = DiacriticsAndTatweel().Replace(name, string.Empty);
        var sb = new System.Text.StringBuilder(stripped.Length);
        foreach (var c in stripped)
        {
            var n = c switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ى' => 'ي',
                'ة' => 'ه',
                _ => c,
            };
            sb.Append(n);
        }
        return WhitespaceRun().Replace(sb.ToString().Trim(), " ");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();

    [GeneratedRegex("[ـً-ْٰ]")]
    private static partial Regex DiacriticsAndTatweel();
}
