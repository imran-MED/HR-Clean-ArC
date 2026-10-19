using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HrAuth.Application.Attendance;
using HrAuth.Application.Common;
using Microsoft.Extensions.Logging;

namespace HrAuth.Infrastructure.HrApi;

/// <summary>
/// <see cref="IAttendanceAdjustmentProvider"/> over the HR API's
/// AdjustAttendance endpoints. The signed-in user's bearer token is forwarded
/// unchanged so the HR API applies that user's adjust-attendance rights.
/// </summary>
public sealed class HrApiAttendanceAdjustmentProvider(
    HttpClient httpClient,
    ILogger<HrApiAttendanceAdjustmentProvider> logger) : IAttendanceAdjustmentProvider
{
    private sealed record AdjustAttendanceRequest(
        [property: JsonPropertyName("employee_id")] string EmployeeId,
        [property: JsonPropertyName("attendance_date")] DateTime AttendanceDate,
        [property: JsonPropertyName("notes")] string? Notes);

    public async Task<Result<string>> AddAsync(
        string employeeId, DateTime attendanceDate, string? notes,
        string bearerToken, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, "api/hr/attendance")
            {
                Content = JsonContent.Create(new AdjustAttendanceRequest(
                    employeeId, attendanceDate, notes)),
            };
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", bearerToken);

            using var response = await httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Attendance adjustment rejected ({Status}) for {Emp} on {Date}: {Body}",
                    (int)response.StatusCode, employeeId, attendanceDate, body);
                return Result<string>.Failure(ExtractError(body)
                    ?? $"HR API rejected the adjustment ({(int)response.StatusCode}).");
            }

            logger.LogInformation(
                "Attendance adjustment accepted for {Emp} on {Date:yyyy-MM-dd}.",
                employeeId, attendanceDate);
            return Result<string>.Success(body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Attendance adjustment call failed.");
            return Result<string>.Failure(
                $"Could not reach the HR API: {ex.Message}");
        }
    }

    public async Task<Result<string>> DeleteAsync(
        string attendanceId, string bearerToken, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"api/hr/attendance/{Uri.EscapeDataString(attendanceId)}");
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", bearerToken);

            using var response = await httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<string>.Failure(ExtractError(body)
                    ?? $"HR API rejected the delete ({(int)response.StatusCode}).");
            }

            return Result<string>.Success(body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Attendance adjustment delete failed.");
            return Result<string>.Failure(
                $"Could not reach the HR API: {ex.Message}");
        }
    }

    /// <summary>Pulls the "message" field out of an ApiResponse body, if any.</summary>
    private static string? ExtractError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }
        }
        catch (JsonException)
        {
            // Not JSON — fall through to the generic message.
        }
        return null;
    }
}
