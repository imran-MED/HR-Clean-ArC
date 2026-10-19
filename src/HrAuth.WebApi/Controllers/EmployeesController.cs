using HrAuth.Application.Employees;
using HrAuth.Application.Employees.Dtos;
using HrAuth.Application.Roles;
using HrAuth.Application.Roles.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace HrAuth.WebApi.Controllers;

/// <summary>
/// Employee directory endpoints — list, single-add, role assignment, CSV
/// bulk import.
/// </summary>
[ApiController]
[Route("api/employees")]
[Produces("application/json")]
public sealed class EmployeesController(
    IEmployeeService employees,
    IEmployeeDirectoryImportService importer,
    IHrApiDirectoryImportService hrApiImporter,
    IRoleService roles) : ControllerBase
{
    /// <summary>Paginated employee list (powers Directory + Roles pages).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(EmployeeDirectoryPage), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? position = null,
        CancellationToken ct = default)
        => Ok(await employees.ListAsync(search, page, pageSize, position, ct));

    /// <summary>Distinct employee positions — populates the directory filter.</summary>
    [HttpGet("positions")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Positions(CancellationToken ct)
        => Ok(await employees.ListPositionsAsync(ct));

    /// <summary>Fetch a single employee by ID (with resolved supervisor name).</summary>
    [HttpGet("{employeeId}")]
    [ProducesResponseType(typeof(EmployeeDirectoryRow), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string employeeId, CancellationToken ct)
    {
        var result = await employees.GetAsync(employeeId, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { error = result.Error });
    }

    /// <summary>
    /// Populate the employee's Position + Supervisor from SmartHCM and persist
    /// them. Returns the updated directory row.
    /// </summary>
    [HttpPost("{employeeId}/sync-from-smarthcm")]
    [ProducesResponseType(typeof(EmployeeDirectoryRow), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SyncFromSmartHcm(
        string employeeId, CancellationToken ct)
    {
        var result = await employees.SyncFromSmartHcmAsync(employeeId, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Manually add a single employee.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeDirectoryRow), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Add(
        [FromBody] AddEmployeeRequest request, CancellationToken ct)
    {
        var result = await employees.AddAsync(request, ct);
        return result.IsSuccess
            ? Created($"/api/employees/{Uri.EscapeDataString(result.Value!.EmployeeId)}",
                result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Replace the role list for one employee.</summary>
    [HttpPut("{employeeId}/roles")]
    [ProducesResponseType(typeof(EmployeeWithRolesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetRoles(
        string employeeId,
        [FromBody] AssignRolesRequest request,
        CancellationToken ct)
    {
        var result = await roles.SetEmployeeRolesAsync(
            employeeId, request.Roles ?? [], ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// Import an employee directory CSV. Each line is
    /// <c>EmployeeId,Name</c> (tab and semicolon are also accepted; an
    /// optional header on line 1 is auto-detected). IDs are normalized to
    /// 8 digits. Existing employees keep their UserName/Roles/LastLoginUtc
    /// — only the display name is updated.
    /// </summary>
    [HttpPost("import-csv")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    [ProducesResponseType(typeof(EmployeeDirectoryImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportCsv(
        IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file was uploaded." });

        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync(ct);

        var result = await importer.ImportAsync(file.FileName, content, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// Import the employee directory straight from the HR API (no file):
    /// fetches every employee (id, name, position) and upserts them into the
    /// local directory. The signed-in user's bearer token is forwarded — the
    /// HR API enforces directory-read authorization.
    /// </summary>
    [HttpPost("import-from-hrapi")]
    [ProducesResponseType(typeof(EmployeeDirectoryImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ImportFromHrApi(CancellationToken ct)
    {
        var header = Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : null;
        if (token is null)
            return Unauthorized(new { error = "Sign in first — no bearer token supplied." });

        var result = await hrApiImporter.ImportAsync(token, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }
}
