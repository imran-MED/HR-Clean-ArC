using System.Security.Cryptography;
using System.Text;

namespace HrAuth.Infrastructure.SmartHcm;

/// <summary>
/// Credential hashing for SmartHCM ASMX calls. The server's password-bearing
/// operations (<c>mob_login</c>, <c>Attendance_GetAttendance</c>,
/// <c>MobGetEmployeeList</c>, <c>Leave_getSupervisorName</c>, …) expect the
/// password as <c>base64(md5(plaintext))</c> — the same scheme the Dawam
/// mobile app uses (<c>AppConfig.hashPassword</c>).
/// </summary>
internal static class SmartHcmCrypto
{
    public static string HashPassword(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToBase64String(bytes);
    }
}
