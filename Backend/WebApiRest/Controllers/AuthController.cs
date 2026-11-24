using Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApiRest.Contracts.Auth;

namespace WebApiRest.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public sealed class AuthController(LoginHandler loginHandler) : ControllerBase
{
    /// <summary>Errors (400 VALIDATION, 401 AUTH-INVALID-CREDENTIALS) are answered by the central error handling.</summary>
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await loginHandler.HandleAsync(
            new LoginCommand(request.Email ?? string.Empty, request.Password ?? string.Empty),
            cancellationToken);

        return Ok(new LoginResponse(result.AccessToken, "Bearer", result.ExpiresAt, result.Role.ToString()));
    }
}
