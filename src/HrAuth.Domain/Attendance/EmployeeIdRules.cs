namespace HrAuth.Domain.Attendance;

/// <summary>
/// Business rules for employee IDs. Across the HR system an employee ID is
/// an 8-digit code; attendance machines often emit it with leading zeros
/// stripped, so it must be normalized before use.
/// </summary>
public static class EmployeeIdRules
{
    /// <summary>The canonical employee-ID width.</summary>
    public const int Width = 8;

    /// <summary>
    /// Normalize an employee ID to <see cref="Width"/> digits by left-padding
    /// with zeros (e.g. <c>0002147 → 00002147</c>). Surrounding whitespace is
    /// trimmed; IDs already at or beyond the width are returned unchanged.
    /// </summary>
    public static string Normalize(string? raw)
    {
        var trimmed = (raw ?? string.Empty).Trim();
        return trimmed.Length >= Width
            ? trimmed
            : trimmed.PadLeft(Width, '0');
    }
}
