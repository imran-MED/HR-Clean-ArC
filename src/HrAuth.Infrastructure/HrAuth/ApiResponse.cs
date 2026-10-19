using System.Text.Json;
using System.Text.Json.Serialization;

namespace HrAuth.Infrastructure.HrAuth;

/// <summary>
/// The standard envelope every HaramAPI endpoint returns:
/// <c>{ success, message, data, errors, warnings, metadata }</c>.
/// <c>data</c> and <c>metadata</c> are left as raw <see cref="JsonElement"/>
/// because the login payload is untyped in the API specification.
/// </summary>
internal sealed class ApiResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    public JsonElement Data { get; set; }

    [JsonPropertyName("metadata")]
    public JsonElement Metadata { get; set; }

    [JsonPropertyName("errors")]
    public List<string>? Errors { get; set; }

    [JsonPropertyName("warnings")]
    public List<string>? Warnings { get; set; }
}
