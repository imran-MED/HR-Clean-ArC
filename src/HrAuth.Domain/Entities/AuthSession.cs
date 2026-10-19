namespace HrAuth.Domain.Entities;

/// <summary>
/// An issued authentication session: the JWT access token, its optional
/// refresh token, the expiry, and the user the token was issued for.
/// </summary>
public sealed class AuthSession
{
    public required string AccessToken { get; init; }

    /// <summary>Null in secure-client mode, where no refresh token is issued.</summary>
    public string? RefreshToken { get; init; }

    /// <summary>Null when the provider does not report an expiry.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    public required AuthenticatedUser User { get; init; }

    /// <summary>True once <see cref="ExpiresAt"/> is in the past.</summary>
    public bool IsExpired =>
        ExpiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow;
}
