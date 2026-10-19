using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HrAuth.Application.Absences;
using HrAuth.Application.Absences.Dtos;
using HrAuth.Application.Common;
using Microsoft.Extensions.Logging;

namespace HrAuth.Infrastructure.HrApi;

/// <summary>
/// <see cref="IAbsenceProvider"/> over the HR API's
/// <c>/api/hr/absences</c>, <c>/api/hr/absence-rates</c>,
/// <c>/api/hr/late-early</c> and <c>/api/hr/attendance-rules</c> endpoints. The HR API runs the SmartHCM queries server-side; the
/// signed-in user's bearer token is forwarded.
/// </summary>
public sealed class HrApiAbsenceProvider(
    HttpClient httpClient,
    ILogger<HrApiAbsenceProvider> logger) : IAbsenceProvider
{
    // The HR API serializes JSON in snake_case (employee_id, attend_date, …).
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private sealed record ApiEnvelope<T>(
        bool Success, string? Message, T? Data, Dictionary<string, JsonElement>? Metadata);

    private sealed record PageData(
        List<AbsenceRecordDto>? Items, int PageNumber, int PageSize,
        int TotalCount, int TotalPages, bool HasPrevious, bool HasNext);

    public async Task<Result<AbsencePageDto>> GetPageAsync(
        AbsenceQuery query, string? bearerToken, CancellationToken ct = default)
    {
        var parameters = new List<string>
        {
            $"includeSynthesized={(query.IncludeSynthesized ? "true" : "false")}",
            $"pageNumber={query.PageNumber}",
            $"pageSize={query.PageSize}",
        };
        AddIfSet(parameters, "payMonth", query.PayMonth);
        AddIfSet(parameters, "from", query.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddIfSet(parameters, "to", query.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddIfSet(parameters, "gender", query.Gender);
        AddIfSet(parameters, "employeeId", query.EmployeeId);

        var result = await GetAsync<PageData>(
            "api/hr/absences?" + string.Join('&', parameters), "absence list", bearerToken, ct);
        if (!result.IsSuccess)
        {
            return Result<AbsencePageDto>.Failure(result.Error);
        }

        var (page, metadata) = result.Value;
        return Result<AbsencePageDto>.Success(new AbsencePageDto
        {
            Items = page?.Items ?? [],
            PageNumber = page?.PageNumber ?? query.PageNumber,
            PageSize = page?.PageSize ?? query.PageSize,
            TotalCount = page?.TotalCount ?? 0,
            TotalPages = page?.TotalPages ?? 0,
            HasPrevious = page?.HasPrevious ?? false,
            HasNext = page?.HasNext ?? false,
            GrandTotalAmount = ReadDecimal(metadata, "grand_total_amount"),
            From = ReadString(metadata, "from"),
            To = ReadString(metadata, "to"),
        });
    }

    public async Task<Result<IReadOnlyList<GradeDto>>> GetGradesAsync(
        string? positionId, string? bearerToken, CancellationToken ct = default)
    {
        var url = "api/hr/absence-rates/grades"
                  + (string.IsNullOrWhiteSpace(positionId)
                      ? string.Empty
                      : $"?positionId={Uri.EscapeDataString(positionId)}");
        return await GetListAsync<GradeDto>(url, "grade lookup", bearerToken, ct);
    }

    public Task<Result<IReadOnlyList<PositionDto>>> GetPositionsAsync(
        bool includeInactive, string? bearerToken, CancellationToken ct = default) =>
        GetListAsync<PositionDto>(
            $"api/hr/absence-rates/positions?includeInactive={(includeInactive ? "true" : "false")}",
            "position lookup", bearerToken, ct);

    public Task<Result<IReadOnlyList<AttendancePeriodDto>>> GetPeriodsAsync(
        string? bearerToken, CancellationToken ct = default) =>
        GetListAsync<AttendancePeriodDto>(
            "api/hr/absence-rates/periods", "period lookup", bearerToken, ct);

    public Task<Result<AbsenceRateMatrixDto>> GetRatesAsync(
        string? payMonth, string? bearerToken, CancellationToken ct = default) =>
        GetValueAsync<AbsenceRateMatrixDto>(
            "api/hr/absence-rates"
            + (string.IsNullOrWhiteSpace(payMonth) ? string.Empty : $"?payMonth={Uri.EscapeDataString(payMonth)}"),
            "rate read", bearerToken, ct);

    public async Task<Result<AbsenceRateMatrixDto>> SaveRatesAsync(
        SaveAbsenceRatesRequest request, string? bearerToken, CancellationToken ct = default)
    {
        var result = await SendAsync<AbsenceRateMatrixDto>(
            HttpMethod.Put, "api/hr/absence-rates", request, "rate save", bearerToken, ct);
        return result.IsSuccess && result.Value.Data is not null
            ? Result<AbsenceRateMatrixDto>.Success(result.Value.Data)
            : Result<AbsenceRateMatrixDto>.Failure(
                result.IsSuccess ? "The HR API returned no rates after saving." : result.Error,
                result.IsSuccess ? null : result.Errors);
    }

    public Task<Result<IReadOnlyList<AbsenceRateCellDto>>> GetRateHistoryAsync(
        int? positionId, int? periodId, string? bearerToken, CancellationToken ct = default)
    {
        var parameters = new List<string>();
        if (positionId is not null) parameters.Add($"positionId={positionId}");
        if (periodId is not null) parameters.Add($"periodId={periodId}");
        return GetListAsync<AbsenceRateCellDto>(
            "api/hr/absence-rates/history" + (parameters.Count > 0 ? "?" + string.Join('&', parameters) : string.Empty),
            "rate history", bearerToken, ct);
    }

    public Task<Result<AbsenceRateRightsDto>> GetRateRightsAsync(
        string? bearerToken, CancellationToken ct = default) =>
        GetValueAsync<AbsenceRateRightsDto>("api/hr/absence-rates/rights", "rate rights", bearerToken, ct);

    public async Task<Result<LateEarlyPageDto>> GetLateEarlyPageAsync(
        LateEarlyQuery query, string? bearerToken, CancellationToken ct = default)
    {
        var parameters = new List<string>
        {
            $"includeLeaveCancelled={(query.IncludeLeaveCancelled ? "true" : "false")}",
            $"pageNumber={query.PageNumber}",
            $"pageSize={query.PageSize}",
        };
        AddIfSet(parameters, "payMonth", query.PayMonth);
        AddIfSet(parameters, "from", query.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddIfSet(parameters, "to", query.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AddIfSet(parameters, "gender", query.Gender);
        AddIfSet(parameters, "employeeId", query.EmployeeId);

        var result = await GetAsync<LateEarlyPageData>(
            "api/hr/late-early?" + string.Join('&', parameters), "late / early list", bearerToken, ct);
        if (!result.IsSuccess)
        {
            return Result<LateEarlyPageDto>.Failure(result.Error);
        }

        var (page, metadata) = result.Value;
        return Result<LateEarlyPageDto>.Success(new LateEarlyPageDto
        {
            Items = page?.Items ?? [],
            PageNumber = page?.PageNumber ?? query.PageNumber,
            PageSize = page?.PageSize ?? query.PageSize,
            TotalCount = page?.TotalCount ?? 0,
            TotalPages = page?.TotalPages ?? 0,
            HasPrevious = page?.HasPrevious ?? false,
            HasNext = page?.HasNext ?? false,
            TotalLateMinutes = ReadDecimal(metadata, "total_late_minutes"),
            TotalEarlyMinutes = ReadDecimal(metadata, "total_early_minutes"),
            TotalMinutes = ReadDecimal(metadata, "total_minutes"),
            CancelledByLeaveCount = (int)ReadDecimal(metadata, "cancelled_by_leave_count"),
            From = ReadString(metadata, "from"),
            To = ReadString(metadata, "to"),
        });
    }

    public Task<Result<AttendanceRulesDto>> GetRulesAsync(
        string? payMonth, string? bearerToken, CancellationToken ct = default) =>
        GetValueAsync<AttendanceRulesDto>(
            "api/hr/attendance-rules"
            + (string.IsNullOrWhiteSpace(payMonth) ? string.Empty : $"?payMonth={Uri.EscapeDataString(payMonth)}"),
            "rule read", bearerToken, ct);

    public async Task<Result<AttendanceRulesDto>> SaveRulesAsync(
        SaveAttendanceRulesRequest request, string? bearerToken, CancellationToken ct = default)
    {
        var result = await SendAsync<AttendanceRulesDto>(
            HttpMethod.Put, "api/hr/attendance-rules", request, "rule save", bearerToken, ct);
        return result.IsSuccess && result.Value.Data is not null
            ? Result<AttendanceRulesDto>.Success(result.Value.Data)
            : Result<AttendanceRulesDto>.Failure(
                result.IsSuccess ? "The HR API returned no rules after saving." : result.Error,
                result.IsSuccess ? null : result.Errors);
    }

    public Task<Result<IReadOnlyList<AttendanceRuleVersionDto>>> GetRuleHistoryAsync(
        string? key, string? bearerToken, CancellationToken ct = default) =>
        GetListAsync<AttendanceRuleVersionDto>(
            "api/hr/attendance-rules/history"
            + (string.IsNullOrWhiteSpace(key) ? string.Empty : $"?key={Uri.EscapeDataString(key)}"),
            "rule history", bearerToken, ct);

    public Task<Result<AbsenceRateRightsDto>> GetRuleRightsAsync(
        string? bearerToken, CancellationToken ct = default) =>
        GetValueAsync<AbsenceRateRightsDto>("api/hr/attendance-rules/rights", "rule rights", bearerToken, ct);

    private sealed record LateEarlyPageData(
        List<LateEarlyRecordDto>? Items, int PageNumber, int PageSize,
        int TotalCount, int TotalPages, bool HasPrevious, bool HasNext);

    private async Task<Result<T>> GetValueAsync<T>(
        string url, string what, string? bearerToken, CancellationToken ct) where T : class
    {
        var result = await GetAsync<T>(url, what, bearerToken, ct);
        if (!result.IsSuccess)
        {
            return Result<T>.Failure(result.Error, result.Errors);
        }

        return result.Value.Data is null
            ? Result<T>.Failure($"The HR API returned an empty {what} response.")
            : Result<T>.Success(result.Value.Data);
    }

    private async Task<Result<IReadOnlyList<T>>> GetListAsync<T>(
        string url, string what, string? bearerToken, CancellationToken ct)
    {
        var result = await GetAsync<List<T>>(url, what, bearerToken, ct);
        return result.IsSuccess
            ? Result<IReadOnlyList<T>>.Success(result.Value.Data ?? [])
            : Result<IReadOnlyList<T>>.Failure(result.Error);
    }

    private Task<Result<(T? Data, Dictionary<string, JsonElement>? Metadata)>> GetAsync<T>(
        string url, string what, string? bearerToken, CancellationToken ct) =>
        SendAsync<T>(HttpMethod.Get, url, body: null, what, bearerToken, ct);

    private async Task<Result<(T? Data, Dictionary<string, JsonElement>? Metadata)>> SendAsync<T>(
        HttpMethod method, string url, object? body, string what, string? bearerToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return Result<(T?, Dictionary<string, JsonElement>?)>.Failure(
                $"Sign in first — the HR API {what} requires your bearer token.");
        }

        try
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            if (body is not null)
            {
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
            }

            using var response = await httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "HR API {What} returned {Status}: {Body}", what, (int)response.StatusCode, responseBody);
                var (message, errors) = ExtractErrors(responseBody);
                return Result<(T?, Dictionary<string, JsonElement>?)>.Failure(
                    message ?? $"The HR API rejected the {what} request ({(int)response.StatusCode}).",
                    errors);
            }

            var envelope = JsonSerializer.Deserialize<ApiEnvelope<T>>(responseBody, JsonOptions);
            if (envelope is null || !envelope.Success)
            {
                return Result<(T?, Dictionary<string, JsonElement>?)>.Failure(
                    envelope?.Message ?? $"The HR API returned an unreadable {what} response.");
            }

            logger.LogInformation("HR API {What}: {Message}", what, envelope.Message);
            return Result<(T?, Dictionary<string, JsonElement>?)>.Success(
                (envelope.Data, envelope.Metadata));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HR API {What} call failed.", what);
            return Result<(T?, Dictionary<string, JsonElement>?)>.Failure(
                $"Could not reach the HR API: {ex.Message}");
        }
    }

    private static void AddIfSet(List<string> parameters, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add($"{name}={Uri.EscapeDataString(value)}");
        }
    }

    private static decimal ReadDecimal(Dictionary<string, JsonElement>? metadata, string key) =>
        metadata is not null
        && metadata.TryGetValue(key, out var value)
        && value.ValueKind == JsonValueKind.Number
            ? value.GetDecimal()
            : 0m;

    private static string? ReadString(Dictionary<string, JsonElement>? metadata, string key) =>
        metadata is not null
        && metadata.TryGetValue(key, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// The HR API envelope's <c>message</c> plus its <c>errors</c> list (the
    /// per-field reasons a save was refused), when the body is JSON.
    /// </summary>
    private static (string? Message, IReadOnlyList<string>? Errors) ExtractErrors(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString()
                : null;
            var errors = root.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Array
                ? e.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString()!)
                    .Where(x => !string.IsNullOrWhiteSpace(x) && x != message)
                    .ToList()
                : null;
            return (message, errors is { Count: > 0 } ? errors : null);
        }
        catch (JsonException)
        {
            // Not JSON — use the generic message.
        }
        return (null, null);
    }
}
