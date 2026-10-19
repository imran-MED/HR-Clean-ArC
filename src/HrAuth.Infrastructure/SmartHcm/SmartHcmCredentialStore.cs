using System.Security.Cryptography;
using System.Text;
using HrAuth.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace HrAuth.Infrastructure.SmartHcm;

/// <summary>
/// In-memory <see cref="ISmartHcmCredentialStore"/>. Only the SmartHCM hash of
/// the password is kept (never the plaintext), keyed by a SHA-256 of the access
/// token so the token itself isn't a cache key either. Entries expire with the
/// token, capped at <see cref="MaxLifetime"/>; a restart clears them all.
/// </summary>
public sealed class SmartHcmCredentialStore(IMemoryCache cache) : ISmartHcmCredentialStore
{
    /// <summary>Upper bound on how long a credential is kept.</summary>
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromHours(12);

    public void Remember(string accessToken, string employeeId, string password,
        DateTimeOffset? expiresAt)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(employeeId))
            return;
        Store(accessToken,
            new SmartHcmCredential(employeeId.Trim(), SmartHcmCrypto.HashPassword(password ?? string.Empty)),
            expiresAt);
    }

    public void Transfer(string oldAccessToken, string newAccessToken, DateTimeOffset? expiresAt)
    {
        if (string.IsNullOrWhiteSpace(oldAccessToken) || string.IsNullOrWhiteSpace(newAccessToken))
            return;
        var existing = Find(oldAccessToken);
        if (existing is null) return;
        cache.Remove(Key(oldAccessToken));
        Store(newAccessToken, existing, expiresAt);
    }

    public SmartHcmCredential? Find(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return null;
        return cache.TryGetValue(Key(accessToken), out SmartHcmCredential? c) ? c : null;
    }

    private void Store(string accessToken, SmartHcmCredential credential, DateTimeOffset? expiresAt)
    {
        var now = DateTimeOffset.UtcNow;
        var expiry = expiresAt is { } e && e > now && e - now < MaxLifetime ? e : now + MaxLifetime;
        cache.Set(Key(accessToken), credential, expiry);
    }

    private static string Key(string accessToken) =>
        "smarthcm-cred:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(accessToken.Trim())));
}
