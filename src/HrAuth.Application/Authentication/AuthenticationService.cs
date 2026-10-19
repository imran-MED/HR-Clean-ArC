using HrAuth.Application.Abstractions;
using HrAuth.Application.Authentication.Dtos;
using HrAuth.Application.Common;
using HrAuth.Application.Persistence;
using HrAuth.Domain.Attendance;
using HrAuth.Domain.Entities;

namespace HrAuth.Application.Authentication;

/// <summary>
/// Coordinates the authentication use cases: validates input, delegates to
/// the <see cref="IHrAuthProvider"/> port, and maps the domain
/// <see cref="AuthSession"/> onto the <see cref="AuthResult"/> DTO the
/// presentation layer returns.
/// </summary>
public sealed class AuthenticationService(
    IHrAuthProvider provider,
    IEmployeeRepository employees,
    ISmartHcmCredentialStore smartHcmCredentials) : IAuthenticationService
{
    public async Task<Result<AuthResult>> LoginAsync(
        LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserName))
            return Result<AuthResult>.Failure("Username is required.");
        if (string.IsNullOrWhiteSpace(request.Password))
            return Result<AuthResult>.Failure("Password is required.");

        var session = await provider.LoginAsync(request.UserName, request.Password, ct);
        var mapped = Map(session);

        // Persist the employee seen at login (best-effort — never blocks login).
        if (mapped.IsSuccess && mapped.Value is { } user)
        {
            // Keep the SmartHCM credential server-side for this session, so the
            // leave screens (apply / approve) can act as this user without the
            // browser ever storing the password — as the Dawam app does.
            smartHcmCredentials.Remember(
                user.AccessToken, EmployeeIdRules.Normalize(user.UserId),
                request.Password, user.ExpiresAt);

            try
            {
                await employees.UpsertAsync(
                    EmployeeIdRules.Normalize(user.UserId),
                    user.Username, user.DisplayName, user.Roles, ct);
            }
            catch
            {
                // The database is optional — ignore persistence failures.
            }
        }

        return mapped;
    }

    public async Task<Result<AuthResult>> RefreshTokenAsync(
        RefreshTokenRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return Result<AuthResult>.Failure("Token is required.");
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Result<AuthResult>.Failure("Refresh token is required.");

        var session = await provider.RefreshTokenAsync(
            request.Token, request.RefreshToken, ct);
        var mapped = Map(session);
        if (mapped.IsSuccess && mapped.Value is { } refreshed)
            smartHcmCredentials.Transfer(request.Token, refreshed.AccessToken, refreshed.ExpiresAt);
        return mapped;
    }

    public Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(token)
            ? Task.FromResult(false)
            : provider.ValidateTokenAsync(token, ct);

    private static Result<AuthResult> Map(Result<AuthSession> result)
    {
        if (!result.IsSuccess || result.Value is null)
            return Result<AuthResult>.Failure(result.Error, result.Errors);

        var session = result.Value;
        return Result<AuthResult>.Success(new AuthResult(
            session.AccessToken,
            session.RefreshToken,
            session.ExpiresAt,
            session.User.UserId,
            session.User.Username,
            session.User.DisplayName,
            session.User.Roles));
    }
}
