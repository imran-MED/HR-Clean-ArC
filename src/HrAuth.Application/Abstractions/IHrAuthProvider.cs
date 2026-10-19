using HrAuth.Application.Common;
using HrAuth.Domain.Entities;

namespace HrAuth.Application.Abstractions;

/// <summary>
/// Port for the external HR identity provider. Implemented in the
/// Infrastructure layer; the Application layer depends only on this
/// abstraction, never on the concrete HTTP client (Dependency Inversion).
/// </summary>
public interface IHrAuthProvider
{
    /// <summary>Authenticate a user and obtain a new session.</summary>
    Task<Result<AuthSession>> LoginAsync(
        string userName, string password, CancellationToken ct = default);

    /// <summary>Exchange a refresh token for a fresh session.</summary>
    Task<Result<AuthSession>> RefreshTokenAsync(
        string token, string refreshToken, CancellationToken ct = default);

    /// <summary>Probe whether a bearer token is still accepted.</summary>
    Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default);
}
