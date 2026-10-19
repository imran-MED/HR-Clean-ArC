namespace HrAuth.Application.Employees.Dtos;

/// <summary>
/// Outcome of importing an employee directory CSV. Returned to the WebApi so
/// the upload page can show what happened.
/// </summary>
public sealed record EmployeeDirectoryImportResult(
    string FileName,
    int LinesParsed,
    int RowsImported,
    int RowsSkipped,
    int TotalEmployeesInDb,
    IReadOnlyList<string> Warnings);
