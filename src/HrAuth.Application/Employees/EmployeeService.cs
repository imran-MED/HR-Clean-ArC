using HrAuth.Application.Common;
using HrAuth.Application.Employees.Dtos;
using HrAuth.Application.Persistence;
using HrAuth.Domain.Attendance;

namespace HrAuth.Application.Employees;

/// <summary>Default <see cref="IEmployeeService"/> backed by EF Core / Postgres.</summary>
public sealed class EmployeeService(
    IEmployeeRepository employees,
    IEmployeeDirectoryProvider directory) : IEmployeeService
{
    private const int MaxPageSize = 200;
    private const int MaxDisplayNameLength = 256;
    private const int MaxUserNameLength = 128;

    private const int MaxPositionLength = 128;

    public async Task<EmployeeDirectoryPage> ListAsync(
        string? search, int page, int pageSize, string? position,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var (total, items) = await employees.ListAsync(
            search, page, pageSize, position, ct);

        // Resolve supervisor names in one batch instead of N per-row lookups.
        var supervisorIds = items
            .Select(e => e.SupervisorEmployeeId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct()
            .ToList();
        var names = supervisorIds.Count == 0
            ? new Dictionary<string, string>()
            : await employees.GetNamesAsync(supervisorIds, ct);

        return new EmployeeDirectoryPage(
            Total: total,
            Page: page,
            PageSize: pageSize,
            Items: items
                .Select(e => new EmployeeDirectoryRow(
                    e.EmployeeId, e.DisplayName, e.UserName, e.Roles,
                    e.Position, e.SupervisorEmployeeId,
                    SupervisorName: ResolveName(e.SupervisorEmployeeId, names)))
                .ToList());
    }

    public Task<IReadOnlyList<string>> ListPositionsAsync(CancellationToken ct = default)
        => employees.ListPositionsAsync(ct);

    public async Task<Result<EmployeeDirectoryRow>> GetAsync(
        string employeeId, CancellationToken ct = default)
    {
        var idError = ValidateId(employeeId);
        if (idError is not null) return Result<EmployeeDirectoryRow>.Failure(idError);

        var normalizedId = EmployeeIdRules.Normalize(employeeId);
        var e = await employees.GetByIdAsync(normalizedId, ct);
        if (e is null)
            return Result<EmployeeDirectoryRow>.Failure(
                $"Employee '{normalizedId}' was not found.");

        var v = e.Value;
        string? supervisorName = null;
        if (!string.IsNullOrEmpty(v.SupervisorEmployeeId))
        {
            var names = await employees.GetNamesAsync([v.SupervisorEmployeeId!], ct);
            supervisorName = ResolveName(v.SupervisorEmployeeId, names);
        }

        return Result<EmployeeDirectoryRow>.Success(new EmployeeDirectoryRow(
            v.EmployeeId, v.DisplayName, v.UserName, v.Roles,
            v.Position, v.SupervisorEmployeeId, supervisorName));
    }

    public async Task<Result<EmployeeDirectoryRow>> SyncFromSmartHcmAsync(
        string employeeId, CancellationToken ct = default)
    {
        var idError = ValidateId(employeeId);
        if (idError is not null) return Result<EmployeeDirectoryRow>.Failure(idError);

        var normalizedId = EmployeeIdRules.Normalize(employeeId);
        var existing = await employees.GetByIdAsync(normalizedId, ct);
        if (existing is null)
            return Result<EmployeeDirectoryRow>.Failure(
                $"Employee '{normalizedId}' is not in the directory.");

        var infoResult = await directory.GetEmployeeInfoAsync(normalizedId, ct);
        if (!infoResult.IsSuccess)
            return Result<EmployeeDirectoryRow>.Failure(infoResult.Error!);

        var info = infoResult.Value!;
        var position = string.IsNullOrWhiteSpace(info.Position) ? null : info.Position!.Trim();
        var supervisorId = string.IsNullOrWhiteSpace(info.SupervisorEmployeeId)
            ? null
            : EmployeeIdRules.Normalize(info.SupervisorEmployeeId!.Trim());

        // Never let an employee become their own supervisor.
        if (supervisorId == normalizedId) supervisorId = null;

        if (position is null && supervisorId is null)
            return Result<EmployeeDirectoryRow>.Failure(
                "SmartHCM did not report a position or supervisor for this employee.");

        await employees.UpdateProfileAsync(normalizedId, position, supervisorId, ct);

        // Re-read; resolve the supervisor name from the local directory, then
        // fall back to the name SmartHCM returned, then the id itself.
        var updated = await employees.GetByIdAsync(normalizedId, ct);
        var v = updated!.Value;
        string? supervisorName = null;
        if (!string.IsNullOrEmpty(v.SupervisorEmployeeId))
        {
            var names = await employees.GetNamesAsync([v.SupervisorEmployeeId!], ct);
            supervisorName = names.TryGetValue(v.SupervisorEmployeeId!, out var n)
                ? n
                : (info.SupervisorName ?? v.SupervisorEmployeeId);
        }

        return Result<EmployeeDirectoryRow>.Success(new EmployeeDirectoryRow(
            v.EmployeeId, v.DisplayName, v.UserName, v.Roles,
            v.Position, v.SupervisorEmployeeId, supervisorName));
    }

    public async Task<Result<EmployeeDirectoryRow>> AddAsync(
        AddEmployeeRequest request, CancellationToken ct = default)
    {
        var idError = ValidateId(request.EmployeeId);
        if (idError is not null) return Result<EmployeeDirectoryRow>.Failure(idError);

        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length == 0)
            return Result<EmployeeDirectoryRow>.Failure("Display name is required.");
        if (displayName.Length > MaxDisplayNameLength)
            return Result<EmployeeDirectoryRow>.Failure(
                $"Display name must be {MaxDisplayNameLength} characters or fewer.");

        var userName = request.UserName?.Trim();
        if (userName is { Length: > MaxUserNameLength })
            return Result<EmployeeDirectoryRow>.Failure(
                $"Username must be {MaxUserNameLength} characters or fewer.");

        var position = request.Position?.Trim();
        if (position is { Length: 0 }) position = null;
        if (position is { Length: > MaxPositionLength })
            return Result<EmployeeDirectoryRow>.Failure(
                $"Position must be {MaxPositionLength} characters or fewer.");

        var normalizedId = EmployeeIdRules.Normalize(request.EmployeeId);

        // Supervisor is a soft reference to another employee. Validate it
        // exists (the "avoids typos" guarantee) and never let an employee be
        // their own supervisor.
        string? supervisorId = null;
        var rawSupervisor = request.SupervisorEmployeeId?.Trim();
        if (!string.IsNullOrEmpty(rawSupervisor))
        {
            var supError = ValidateId(rawSupervisor);
            if (supError is not null)
                return Result<EmployeeDirectoryRow>.Failure(
                    $"Supervisor ID: {supError}");

            supervisorId = EmployeeIdRules.Normalize(rawSupervisor);
            if (supervisorId == normalizedId)
                return Result<EmployeeDirectoryRow>.Failure(
                    "An employee cannot be their own supervisor.");

            var supervisor = await employees.GetByIdAsync(supervisorId, ct);
            if (supervisor is null)
                return Result<EmployeeDirectoryRow>.Failure(
                    $"Supervisor '{supervisorId}' is not in the directory. "
                    + "Add them first.");
        }

        var added = await employees.AddAsync(
            normalizedId, displayName, userName, position, supervisorId, ct);
        if (!added)
            return Result<EmployeeDirectoryRow>.Failure(
                $"Employee '{normalizedId}' already exists.");

        string? supervisorName = null;
        if (supervisorId is not null)
        {
            var names = await employees.GetNamesAsync([supervisorId], ct);
            supervisorName = ResolveName(supervisorId, names);
        }

        return Result<EmployeeDirectoryRow>.Success(new EmployeeDirectoryRow(
            normalizedId, displayName, userName, [],
            position, supervisorId, supervisorName));
    }

    /// <summary>Resolve a supervisor ID to a display name, falling back to the
    /// ID itself when no name is on record (so the UI never shows a blank).</summary>
    private static string? ResolveName(
        string? supervisorId, IReadOnlyDictionary<string, string> names)
    {
        if (string.IsNullOrEmpty(supervisorId)) return null;
        return names.TryGetValue(supervisorId, out var name) ? name : supervisorId;
    }

    private static string? ValidateId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Employee ID is required.";
        var trimmed = raw.Trim();
        if (!trimmed.All(char.IsDigit))
            return "Employee ID must contain digits only.";
        if (trimmed.Length > 32)
            return "Employee ID is too long.";
        return null;
    }
}
