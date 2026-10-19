using System.Net.Http.Headers;
using System.Text.Json;
using HrAuth.Application.Common;
using HrAuth.Application.Employees;
using Microsoft.Extensions.Logging;

namespace HrAuth.Infrastructure.HrApi;

/// <summary>
/// <see cref="IHrApiDirectorySource"/> over the HR API's
/// <c>GET /api/hr/employee/directory</c> endpoint. Forwards the signed-in
/// user's bearer token; the HR API enforces directory-read authorization.
/// </summary>
public sealed class HrApiDirectorySource(
    HttpClient httpClient,
    ILogger<HrApiDirectorySource> logger) : IHrApiDirectorySource
{
    // The HR API serializes JSON in snake_case (employee_id, employee_name, …).
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private sealed record DirectoryEntry(
        string? EmployeeId, string? EmployeeName, string? Designation);

    private sealed record ApiEnvelope(
        bool Success, string? Message, List<DirectoryEntry>? Data);

    public async Task<Result<IReadOnlyList<HrApiDirectoryEmployee>>> FetchAsync(
        string bearerToken, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, "api/hr/employee/directory");
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", bearerToken);

            using var response = await httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "HR API directory returned {Status}: {Body}",
                    (int)response.StatusCode, body);
                return Result<IReadOnlyList<HrApiDirectoryEmployee>>.Failure(
                    ExtractMessage(body)
                    ?? $"The HR API rejected the directory request ({(int)response.StatusCode}).");
            }

            var envelope = JsonSerializer.Deserialize<ApiEnvelope>(body, JsonOptions);
            if (envelope is null || !envelope.Success)
            {
                return Result<IReadOnlyList<HrApiDirectoryEmployee>>.Failure(
                    envelope?.Message ?? "The HR API returned an unreadable directory response.");
            }

            var employees = (envelope.Data ?? [])
                .Where(e => !string.IsNullOrWhiteSpace(e.EmployeeId))
                .Select(e => new HrApiDirectoryEmployee(
                    e.EmployeeId!.Trim(),
                    e.EmployeeName?.Trim() ?? string.Empty,
                    string.IsNullOrWhiteSpace(e.Designation) ? null : e.Designation.Trim()))
                .ToList();

            logger.LogInformation(
                "HR API directory: {Count} employee(s) fetched.", employees.Count);
            return Result<IReadOnlyList<HrApiDirectoryEmployee>>.Success(employees);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HR API directory fetch failed.");
            return Result<IReadOnlyList<HrApiDirectoryEmployee>>.Failure(
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
