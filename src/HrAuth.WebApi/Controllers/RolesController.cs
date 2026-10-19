using HrAuth.Application.Roles;
using HrAuth.Application.Roles.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace HrAuth.WebApi.Controllers;

/// <summary>Role catalog management (backs the Roles → Catalog tab).</summary>
[ApiController]
[Route("api/roles")]
[Produces("application/json")]
public sealed class RolesController(IRoleService roles) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await roles.ListRolesAsync(ct));

    [HttpPost]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Add(
        [FromBody] RoleUpsertRequest request, CancellationToken ct)
    {
        var result = await roles.AddRoleAsync(request, ct);
        return result.IsSuccess
            ? Created($"/api/roles/{Uri.EscapeDataString(result.Value!.Name)}", result.Value)
            : BadRequest(new { error = result.Error });
    }

    [HttpPut("{name}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        string name, [FromBody] RoleUpsertRequest request, CancellationToken ct)
    {
        var result = await roles.UpdateRoleAsync(name, request, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    [HttpDelete("{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(string name, CancellationToken ct)
    {
        var result = await roles.DeleteRoleAsync(name, ct);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(new { error = result.Error });
    }
}
