using System.Text.Json;
using HrAuth.Domain.Entities;

namespace HrAuth.Infrastructure.HrAuth;

/// <summary>
/// Builds an <see cref="AuthSession"/> from the untyped <c>data</c> /
/// <c>metadata</c> of an HrAuth response. The login payload is untyped in
/// the API spec, so every field is picked defensively, tolerating both
/// snake_case and camelCase key variants.
/// </summary>
internal static class HrAuthSessionParser
{
    public static AuthSession? Parse(ApiResponse envelope)
    {
        var sources = new[] { envelope.Data, envelope.Metadata };

        var token = PickString(sources, "token", "access_token", "accessToken", "jwt");
        if (string.IsNullOrEmpty(token))
            return null;

        var refreshToken = PickString(sources, "refresh_token", "refreshToken");
        var userId = PickString(sources,
            "user_id", "userId", "id", "employee_id", "employeeId") ?? string.Empty;
        var username = PickString(sources,
            "username", "user_name", "userName") ?? string.Empty;
        var displayName = PickString(sources,
            "employee_name", "employeeName", "display_name", "displayName",
            "full_name", "name");
        var expiresRaw = PickString(sources, "expires_at", "expiresAt", "expiration");

        DateTimeOffset? expiresAt =
            DateTimeOffset.TryParse(expiresRaw, out var parsed) ? parsed : null;

        return new AuthSession
        {
            AccessToken = token,
            RefreshToken = string.IsNullOrEmpty(refreshToken) ? null : refreshToken,
            ExpiresAt = expiresAt,
            User = new AuthenticatedUser
            {
                UserId = userId,
                Username = username,
                DisplayName = displayName,
                Roles = PickRoles(sources),
            },
        };
    }

    /// <summary>First non-empty value found for any of <paramref name="keys"/>.</summary>
    private static string? PickString(IEnumerable<JsonElement> sources, params string[] keys)
    {
        foreach (var source in sources)
        {
            if (source.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var key in keys)
            {
                if (!source.TryGetProperty(key, out var value))
                    continue;

                var text = value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Number => value.ToString(),
                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                    _ => value.ToString(),
                };
                if (!string.IsNullOrEmpty(text))
                    return text;
            }
        }
        return null;
    }

    private static IReadOnlyList<string> PickRoles(IEnumerable<JsonElement> sources)
    {
        foreach (var source in sources)
        {
            if (source.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var key in new[] { "roles", "Roles" })
            {
                if (source.TryGetProperty(key, out var value) &&
                    value.ValueKind == JsonValueKind.Array)
                {
                    return value.EnumerateArray()
                        .Select(e => e.ValueKind == JsonValueKind.String
                            ? e.GetString()
                            : e.ToString())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Select(s => s!)
                        .ToList();
                }
            }
        }
        return [];
    }
}
