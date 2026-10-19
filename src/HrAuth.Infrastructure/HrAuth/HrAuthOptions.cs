namespace HrAuth.Infrastructure.HrAuth;

/// <summary>
/// Strongly-typed settings bound from the "HrAuth" configuration section.
/// </summary>
public sealed class HrAuthOptions
{
    public const string SectionName = "HrAuth";

    /// <summary>Base URL of the HR identity API.</summary>
    public string BaseUrl { get; set; } = "https://hrapi.qm.edu.sa";

    /// <summary>
    /// Optional gateway token. When set, it is sent as the
    /// <c>X-Hr-Login-Gateway-Token</c> header to switch the server into
    /// secure-client login mode (longer-lived token, no refresh token).
    /// </summary>
    public string? GatewayToken { get; set; }

    /// <summary>
    /// Accept the server's self-signed / untrusted TLS certificate.
    /// Defaults to true because the legacy QM infrastructure ships one.
    /// </summary>
    public bool AcceptUntrustedCertificate { get; set; } = true;

    /// <summary>HTTP request timeout, in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
