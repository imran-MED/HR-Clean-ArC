using HrAuth.Application.Rosters;
using HrAuth.Application.Rosters.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace HrAuth.WebApi.Controllers;

/// <summary>Employee shift-roster endpoints.</summary>
[ApiController]
[Route("api/roster")]
[Produces("application/json")]
public sealed class RosterController(IRosterService roster) : ControllerBase
{
    /// <summary>
    /// Get an employee's shift roster for a date (defaults to today). The
    /// employee ID is normalized to 8 digits before lookup. Example:
    /// <c>GET /api/roster/00000314?date=2026-05-19</c>.
    /// </summary>
    [HttpGet("{employeeId}")]
    [ProducesResponseType(typeof(RosterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Get(
        string employeeId, [FromQuery] DateOnly? date, CancellationToken ct)
    {
        var result = await roster.GetRosterAsync(employeeId, date, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : StatusCode(StatusCodes.Status502BadGateway,
                new { error = result.Error });
    }
}
