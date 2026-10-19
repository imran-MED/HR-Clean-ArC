using System.Net.Http.Json;
using System.Text.Json;
using HrAuth.Application.Abstractions;
using HrAuth.Application.Common;
using HrAuth.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrAuth.Infrastructure.HrAuth;

/// <summary>
/// <see cref="IHrAuthProvider"/> backed by the HaramAPI HrAuth REST endpoints:
/// <c>/api/hr/auth/login</c>, <c>/refresh-token</c> and <c>/validate-token</c>.
/// </summary>
public sealed class HrAuthProvider(
    HttpClient httpClient,
    IOptions<HrAuthOptions> options,
    ILogger<HrAuthProvider> logger) : IHrAuthProvider
{
    private readonly HrAuthOptions _options = options.Value;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public Task<Result<AuthSession>> LoginAsync(
        string userName, string password, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/hr/auth/login")
        {
            Content = JsonContent.Create(new { user_name = userName, password }),
        };
        if (!string.IsNullOrWhiteSpace(_options.GatewayToken))
        {
            request.Headers.TryAddWithoutValidation(
                "X-Hr-Login-Gateway-Token", _options.GatewayToken);
        }

        return SendAsync(request, "login", ct);
    }

    public Task<Result<AuthSession>> RefreshTokenAsync(
        string token, string refreshToken, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/hr/auth/refresh-token")
        {
            Content = JsonContent.Create(new { token, refresh_token = refreshToken }),
        };
        return SendAsync(request, "refresh-token", ct);
    }

    public async Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        try
        {
            using var request =
                new HttpRequestMessage(HttpMethod.Post, "/api/hr/auth/validate-token");
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");

            using var response = await httpClient.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "HrAuth validate-token call failed");
            return false;
        }
    }

    /// <summary>
    /// Sends the request, parses the standard envelope, and maps it onto a
    /// <see cref="Result{T}"/>. Never throws — transport failures become a
    /// failed result so the call doesn't cross the layer boundary as an
    /// exception.
    /// </summary>
    private async Task<Result<AuthSession>> SendAsync(
        HttpRequestMessage request, string operation, CancellationToken ct)
    {
        try
        {
            using (request)
            using (var response = await httpClient.SendAsync(request, ct))
            {
                var body = await response.Content.ReadAsStringAsync(ct);

                if (string.IsNullOrWhiteSpace(body))
                {
                    return Result<AuthSession>.Failure(
                        $"HrAuth {operation} returned an empty response " +
                        $"({(int)response.StatusCode}).");
                }

                ApiResponse? envelope;
                try
                {
                    envelope = JsonSerializer.Deserialize<ApiResponse>(body, JsonOptions);
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "HrAuth {Operation} response was not valid JSON",
                        operation);
                    return Result<AuthSession>.Failure(
                        $"HrAuth {operation} returned an unreadable response.");
                }

                if (envelope is null)
                {
                    return Result<AuthSession>.Failure(
                        $"HrAuth {operation} returned an empty envelope.");
                }

                if (!envelope.Success)
                {
                    var message = !string.IsNullOrWhiteSpace(envelope.Message)
                        ? envelope.Message!
                        : envelope.Errors is { Count: > 0 } errs
                            ? errs[0]
                            : "Authentication failed.";
                    logger.LogWarning("HrAuth {Operation} was rejected: {Message}",
                        operation, message);
                    return Result<AuthSession>.Failure(message, envelope.Errors);
                }

                var session = HrAuthSessionParser.Parse(envelope);
                if (session is null)
                {
                    return Result<AuthSession>.Failure(
                        $"HrAuth {operation} succeeded but returned no token.");
                }

                logger.LogInformation("HrAuth {Operation} succeeded for user {User}",
                    operation, session.User.Username);
                return Result<AuthSession>.Success(session);
            }
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HrAuth {Operation} call failed", operation);
            return Result<AuthSession>.Failure(
                $"Could not reach the HR authentication service: {ex.Message}");
        }
    }
}
