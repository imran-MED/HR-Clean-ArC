using System.Text.Json;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Options;

namespace HrAuth.Infrastructure.SmartHcm;

/// <summary>
/// Minimal HTTP client for the legacy SmartHCM ASMX web service. Operations
/// are invoked as <c>application/x-www-form-urlencoded</c> POSTs; responses
/// are XML whose root <c>&lt;string&gt;</c> element wraps a JSON payload.
/// </summary>
public sealed class SmartHcmClient(
    HttpClient httpClient, IOptions<SmartHcmOptions> options)
{
    private readonly string _baseUrl = options.Value.BaseUrl.TrimEnd('/');

    /// <summary>
    /// POST an ASMX operation with form-urlencoded parameters; returns the
    /// raw XML response body.
    /// </summary>
    public async Task<string> CallAsync(
        string operation,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        using var content = new FormUrlEncodedContent(parameters);
        using var response = await httpClient.PostAsync(
            $"{_baseUrl}/{operation}", content, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>
    /// GET an ASMX operation with query-string parameters; returns the raw
    /// XML response body. Some read-only operations (e.g.
    /// <c>Attendance_GetAttendance</c>) are only exposed over HTTP GET, which
    /// is how the Dawam mobile app invokes them.
    /// </summary>
    public async Task<string> CallGetAsync(
        string operation,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        foreach (var (key, value) in parameters)
            query[key] = value;

        var url = $"{_baseUrl}/{operation}?{query}";
        using var response = await httpClient.GetAsync(url, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>
    /// POST an operation as <c>multipart/form-data</c>: the string parameters
    /// as form fields plus the given files. Only usable with operations that
    /// declare no parameters and read <c>Request.Form</c> / <c>Request.Files</c>
    /// themselves (e.g. <c>SaveLeave</c>) — ASMX rejects multipart for
    /// operations with declared parameters ("Request format is invalid").
    /// The server walks <c>Request.Files</c> positionally, so file order is
    /// kept and the field name is arbitrary.
    /// </summary>
    public async Task<string> CallMultipartAsync(
        string operation,
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyList<(string FileName, string ContentType, byte[] Content)> files,
        CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        foreach (var (key, value) in parameters)
            content.Add(new StringContent(value ?? string.Empty), key);
        foreach (var file in files)
        {
            var part = new ByteArrayContent(file.Content);
            part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
            content.Add(part, "files", file.FileName);
        }

        using var response = await httpClient.PostAsync(
            $"{_baseUrl}/{operation}", content, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>
    /// Extract the inner text of an ASMX <c>&lt;string&gt;</c> response — for
    /// SmartHCM this is a JSON document carried as XML text.
    /// </summary>
    public static string ParseStringResponse(string xml)
    {
        try
        {
            return XDocument.Parse(xml).Root?.Value ?? string.Empty;
        }
        catch (XmlException)
        {
            return xml;
        }
    }

    /// <summary>
    /// Parse an ASMX DataTable/DataSet XML response into rows. SmartHCM ships
    /// rows under <c>&lt;DocumentElement&gt;</c> (error payloads such as
    /// <c>tblError</c>) and <c>&lt;NewDataSet&gt;</c> (real DataSets); both are
    /// handled. The inline <c>xs:schema</c> element is skipped. Each row maps
    /// child element local-name → inner text.
    /// </summary>
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> ParseDataTable(string xml)
    {
        var rows = new List<IReadOnlyDictionary<string, string>>();
        try
        {
            var doc = XDocument.Parse(xml);
            var wrappers = doc.Descendants()
                .Where(e => e.Name.LocalName is "DocumentElement" or "NewDataSet");
            foreach (var wrapper in wrappers)
            {
                foreach (var rowEl in wrapper.Elements())
                {
                    if (rowEl.Name.LocalName == "schema")
                        continue;
                    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var child in rowEl.Elements())
                        row[child.Name.LocalName] = child.Value;
                    if (row.Count > 0)
                        rows.Add(row);
                }
            }
        }
        catch (XmlException)
        {
            // Unparseable body → no rows.
        }
        return rows;
    }

    /// <summary>
    /// Parse a <c>&lt;string&gt;</c> response whose inner text is a JSON array
    /// of row objects — the shape <c>JsonConvert.SerializeObject(DataTable)</c>
    /// produces (e.g. <c>MobGetEmployeeList</c>). Each object becomes a
    /// string→string row (scalar values stringified, nulls dropped).
    /// </summary>
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> ParseJsonRows(string xml)
    {
        var rows = new List<IReadOnlyDictionary<string, string>>();
        var inner = ParseStringResponse(xml).Trim();
        if (inner.Length == 0 || inner[0] != '[')
            return rows;
        try
        {
            using var doc = JsonDocument.Parse(inner);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return rows;
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                    continue;
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in element.EnumerateObject())
                {
                    var value = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.Number => prop.Value.ToString(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => null,
                    };
                    if (!string.IsNullOrEmpty(value))
                        row[prop.Name] = value;
                }
                if (row.Count > 0)
                    rows.Add(row);
            }
        }
        catch (JsonException)
        {
            // Unparseable JSON → no rows.
        }
        return rows;
    }
}
