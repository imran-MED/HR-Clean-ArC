using HrAuth.Application.Leaves;
using HrAuth.Application.Leaves.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace HrAuth.WebApi.Controllers;

/// <summary>
/// Employee leave endpoints. Leaves are fetched from SmartHCM
/// (<c>Hrs_GetPendingLeaves</c>) per employee using that employee's own
/// password, stored locally, and used by reconciliation to mark days on leave.
/// </summary>
[ApiController]
[Route("api/leaves")]
[Produces("application/json")]
public sealed class LeavesController(ILeaveService leaves) : ControllerBase
{
    /// <summary>
    /// Fetch an employee's leaves from SmartHCM and store them. Requires that
    /// employee's <paramref name="password"/> (plaintext; hashed before sending).
    /// </summary>
    [HttpGet("fetch")]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Fetch(
        [FromQuery] string employeeId, [FromQuery] string password,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            return BadRequest(new { error = "employeeId is required." });

        var result = await leaves.FetchAndStoreAsync(
            employeeId, password ?? string.Empty, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>List the leaves already stored for an employee.</summary>
    [HttpGet("{employeeId}")]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(string employeeId, CancellationToken ct)
        => Ok(await leaves.ListStoredAsync(employeeId, ct));
}
