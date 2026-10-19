using HrAuth.Application.Authentication;
using HrAuth.Application.Authentication.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace HrAuth.WebApi.Controllers;

/// <summary>
/// Authentication endpoints. This API wraps the HR identity provider behind
/// its own contract, so callers never talk to the HR service directly.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(IAuthenticationService authentication)
    : ControllerBase
{
    /// <summary>Authenticate a user and issue a JWT session.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await authentication.LoginAsync(request, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(new { error = result.Error, errors = result.Errors });
    }

    /// <summary>Exchange a refresh token for a fresh JWT pair.</summary>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await authentication.RefreshTokenAsync(request, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(new { error = result.Error, errors = result.Errors });
    }

    /// <summary>
    /// Check whether the bearer token in the <c>Authorization</c> header is
    /// still valid.
    /// </summary>
    [HttpPost("validate-token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateToken(CancellationToken ct)
    {
        var header = Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : header.Trim();

        var isValid = await authentication.ValidateTokenAsync(token, ct);
        return Ok(new { valid = isValid });
    }
}
