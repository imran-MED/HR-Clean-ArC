using HrAuth.Application.Authentication.Dtos;
using HrAuth.Application.Common;

namespace HrAuth.Application.Authentication;

/// <summary>Authentication use cases exposed to the presentation layer.</summary>
public interface IAuthenticationService
{
    Task<Result<AuthResult>> LoginAsync(
        LoginRequest request, CancellationToken ct = default);

    Task<Result<AuthResult>> RefreshTokenAsync(
        RefreshTokenRequest request, CancellationToken ct = default);

    Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default);
}
