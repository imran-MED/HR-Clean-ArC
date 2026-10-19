namespace HrAuth.Application.Authentication.Dtos;

/// <summary>The token pair and user identity returned to the API caller.</summary>
public sealed record AuthResult(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset? ExpiresAt,
    string UserId,
    string Username,
    string? DisplayName,
    IReadOnlyList<string> Roles);
