using System.Net.Http.Headers;
using System.Text.Json;
using HrAuth.Application.Common;
using HrAuth.Application.Reports;
using HrAuth.Application.Reports.Dtos;
using Microsoft.Extensions.Logging;

namespace HrAuth.Infrastructure.HrApi;

/// <summary>
/// <see cref="IMissingPunchReportProvider"/> over the HR API's
/// <c>GET /api/hr/reports/missing-punches</c> endpoint. The signed-in user's
/// bearer token is forwarded.
/// </summary>
public sealed class HrApiMissingPunchReportProvider(
    HttpClient httpClient,
    ILogger<HrApiMissingPunchReportProvider> logger) : IMissingPunchReportProvider
{
    // The HR API serializes JSON in snake_case (employee_id, att_date, …).
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private sealed record ApiEnvelope(
        bool Success, string? Message, List<MissingPunchRowDto>? Data);

    public async Task<Result<IReadOnlyList<MissingPunchRowDto>>> GetAsync(
        DateOnly from, DateOnly to, string? employeeId, string? bearerToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return Result<IReadOnlyList<MissingPunchRowDto>>.Failure(
                "Sign in first — the HR API report requires your bearer token.");
        }

        try
        {
            var url = $"api/hr/reports/missing-punches?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}"
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
                    "HR API missing-punch report returned {Status}: {Body}",
                    (int)response.StatusCode, body);
                return Result<IReadOnlyList<MissingPunchRowDto>>.Failure(
                    ExtractMessage(body)
                    ?? $"The HR API rejected the report request ({(int)response.StatusCode}).");
            }

            var envelope = JsonSerializer.Deserialize<ApiEnvelope>(body, JsonOptions);
            if (envelope is null || !envelope.Success)
            {
                return Result<IReadOnlyList<MissingPunchRowDto>>.Failure(
                    envelope?.Message ?? "The HR API returned an unreadable report response.");
            }

            var rows = envelope.Data ?? [];
            logger.LogInformation(
                "HR API missing-punch report: {Count} row(s) for {From}..{To}.",
                rows.Count, from, to);
            return Result<IReadOnlyList<MissingPunchRowDto>>.Success(rows);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HR API missing-punch report call failed.");
            return Result<IReadOnlyList<MissingPunchRowDto>>.Failure(
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
