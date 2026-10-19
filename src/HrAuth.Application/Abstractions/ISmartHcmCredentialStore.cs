namespace HrAuth.Application.Abstractions;

/// <summary>
/// The SmartHCM identity of a signed-in user: their 8-digit employee id and the
/// password already hashed into the form SmartHCM expects
/// (<c>base64(md5(password))</c>). The plaintext password is never kept.
/// </summary>
public sealed record SmartHcmCredential(string EmployeeId, string PasswordHash);

/// <summary>
/// Server-side, in-memory link between an issued access token and the
/// SmartHCM credential of the user who signed in with it. SmartHCM's leave
/// operations (apply, approve, withdraw …) need the caller's own password on
/// every call — exactly like the Dawam mobile app, which keeps it for the
/// session. Keeping it on the server means the browser never stores it: the
/// web app only sends its bearer token, and the credential is resolved here.
/// Entries expire with the token (and are lost on restart → sign in again).
/// </summary>
public interface ISmartHcmCredentialStore
{
    /// <summary>Remember the credential for <paramref name="accessToken"/>.</summary>
    void Remember(string accessToken, string employeeId, string password,
        DateTimeOffset? expiresAt);

    /// <summary>Move a credential to a refreshed token (old token forgotten).</summary>
    void Transfer(string oldAccessToken, string newAccessToken, DateTimeOffset? expiresAt);

    /// <summary>The credential for <paramref name="accessToken"/>, or null.</summary>
    SmartHcmCredential? Find(string accessToken);
}
