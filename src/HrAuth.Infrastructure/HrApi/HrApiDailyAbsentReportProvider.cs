using System.Net.Http.Headers;
using System.Text.Json;
using HrAuth.Application.Common;
using HrAuth.Application.Reports;
using HrAuth.Application.Reports.Dtos;
using Microsoft.Extensions.Logging;

namespace HrAuth.Infrastructure.HrApi;

/// <summary>
/// <see cref="IDailyAbsentReportProvider"/> over the HR API's
/// <c>GET /api/hr/reports/daily-absent</c> endpoint — used when this app runs
/// where the SmartHCM Oracle database is not reachable; the HR API executes
/// the report server-side. The signed-in user's bearer token is forwarded.
/// </summary>
public sealed class HrApiDailyAbsentReportProvider(
    HttpClient httpClient,
    ILogger<HrApiDailyAbsentReportProvider> logger) : IDailyAbsentReportProvider
{
    // The HR API serializes JSON in snake_case (employee_id, attend_date, …).
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private sealed record ApiEnvelope(
        bool Success, string? Message, List<DailyAbsentRowDto>? Data);

    public async Task<Result<IReadOnlyList<DailyAbsentRowDto>>> GetAsync(
        DateOnly from, DateOnly to, string? employeeId, string? bearerToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return Result<IReadOnlyList<DailyAbsentRowDto>>.Failure(
                "Sign in first — the HR API report requires your bearer token.");
        }

        try
        {
            var url = $"api/hr/reports/daily-absent?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}"
                      + (string.IsNullOrWhiteSpace(employeeId)
                          ? string.Empty
                          : $"&employeeId={Uri.EscapeDataString(employeeId)}");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", bearerToken);

            using var response = await httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "HR API daily-absent report returned {Status}: {Body}",
                    (int)response.StatusCode, body);
                return Result<IReadOnlyList<DailyAbsentRowDto>>.Failure(
                    ExtractMessage(body)
                    ?? $"The HR API rejected the report request ({(int)response.StatusCode}).");
            }

            var envelope = JsonSerializer.Deserialize<ApiEnvelope>(body, JsonOptions);
            if (envelope is null || !envelope.Success)
            {
                return Result<IReadOnlyList<DailyAbsentRowDto>>.Failure(
                    envelope?.Message ?? "The HR API returned an unreadable report response.");
            }

            var rows = envelope.Data ?? [];
            logger.LogInformation(
                "HR API daily-absent report: {Count} row(s) for {From}..{To}.",
                rows.Count, from, to);
            return Result<IReadOnlyList<DailyAbsentRowDto>>.Success(rows);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HR API daily-absent report call failed.");
            return Result<IReadOnlyList<DailyAbsentRowDto>>.Failure(
                $"Could not reach the HR API: {ex.Message}");
        }
    }

    private static string? ExtractMessage(string body)
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
            // Not JSON — use the generic message.
        }
        return null;
    }
}
