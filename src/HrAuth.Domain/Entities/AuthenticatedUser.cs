namespace HrAuth.Domain.Entities;

/// <summary>
/// A user who has been authenticated against the HR identity provider.
/// Pure domain object — no framework or transport concerns.
/// </summary>
public sealed class AuthenticatedUser
{
    public required string UserId { get; init; }

    public required string Username { get; init; }

    /// <summary>Human-readable name for display (may be null).</summary>
    public string? DisplayName { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>True when the user holds the given role (case-insensitive).</summary>
    public bool IsInRole(string role) =>
        Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));

    /// <summary>True when any role name contains "admin".</summary>
    public bool IsAdministrator =>
        Roles.Any(r => r.Contains("admin", StringComparison.OrdinalIgnoreCase));
}
