using HrAuth.Application.Common;

namespace HrAuth.Application.Employees;

/// <summary>
/// Port for an external HR directory that can supply an employee's
/// organizational attributes (position / supervisor). Implemented in the
/// Infrastructure layer against the legacy SmartHCM ASMX service.
/// </summary>
public interface IEmployeeDirectoryProvider
{
    /// <summary>
    /// Look up the position and immediate supervisor for one employee.
    /// A successful result may still carry null fields when the source did
    /// not report them.
    /// </summary>
    Task<Result<SmartHcmEmployeeInfo>> GetEmployeeInfoAsync(
        string employeeId, CancellationToken ct = default);
}

/// <summary>
/// Organizational attributes resolved from SmartHCM for a single employee.
/// Any field may be null when the source did not report it.
/// </summary>
public sealed record SmartHcmEmployeeInfo(
    string? Position,
    string? SupervisorEmployeeId,
    string? SupervisorName);
