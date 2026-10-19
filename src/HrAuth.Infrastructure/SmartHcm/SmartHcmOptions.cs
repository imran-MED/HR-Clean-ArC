namespace HrAuth.Infrastructure.SmartHcm;

/// <summary>
/// Settings for the legacy SmartHCM ASMX web service, bound from the
/// "SmartHcm" configuration section.
/// </summary>
public sealed class SmartHcmOptions
{
    public const string SectionName = "SmartHcm";

    /// <summary>Base URL of the SmartHCM ASMX endpoint.</summary>
    public string BaseUrl { get; set; } = "https://hr.qm.edu.sa/ws/SmartHCMWS.asmx";

    /// <summary>
    /// The ASMX token key — required by every SmartHCM operation. Keep this
    /// out of source control: supply it via <c>appsettings.Development.json</c>,
    /// an environment variable (<c>SmartHcm__TokenKey</c>), or user-secrets.
    /// </summary>
    public string TokenKey { get; set; } = string.Empty;

    /// <summary>
    /// The encrypted integration "security code" required by the
    /// <c>*_SecurityCode</c> operations (e.g.
    /// <c>GetEmployeeDetailListById_SecurityCode</c>). Must match the server's
    /// <c>WEBSERVICE_SECURITY_CODE_INTEGRATION</c> setup. Supply via
    /// <c>appsettings.Development.json</c>, env var
    /// (<c>SmartHcm__SecurityKey</c>), or user-secrets — never source control.
    /// Empty disables SmartHCM position lookup.
    /// </summary>
    public string SecurityKey { get; set; } = string.Empty;

    /// <summary>Language code passed to SmartHCM read operations.</summary>
    public string LanguageCode { get; set; } = "en-US";

    /// <summary>
    /// <c>GetSuperVisor</c> resolves the *leave-workflow* supervisor and needs a
    /// leave-type + applied-days context. These defaults drive that lookup when
    /// the employee-detail row does not itself carry a supervisor. An empty
    /// leave type disables the GetSuperVisor fallback.
    /// </summary>
    public string SupervisorLeaveType { get; set; } = string.Empty;
    public string SupervisorAppliedDays { get; set; } = "1";

    /// <summary>
    /// A manager/admin employee id whose span covers the staff to sync. Used
    /// (with <see cref="DirectoryAdminPassword"/>) by the API directory path
    /// (<c>MobGetEmployeeList</c>) to pull Position for that manager's
    /// employees — the same call the Dawam app's admin dashboard makes. No
    /// per-employee passwords, no security code, no Oracle. When set, the API
    /// directory provider is preferred over the Oracle / *_SecurityCode paths.
    /// Supply via appsettings.Development.json / env / user-secrets only.
    /// </summary>
    public string DirectoryAdminEmpId { get; set; } = string.Empty;

    /// <summary>
    /// The plaintext password for <see cref="DirectoryAdminEmpId"/>. Hashed to
    /// base64(md5(password)) at call time, as SmartHCM expects. Secret — never
    /// source control.
    /// </summary>
    public string DirectoryAdminPassword { get; set; } = string.Empty;

    /// <summary>
    /// Accept the server's self-signed / untrusted TLS certificate. The
    /// legacy QM infrastructure ships one, so this defaults to true.
    /// </summary>
    public bool AcceptUntrustedCertificate { get; set; } = true;

    /// <summary>HTTP request timeout, in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
