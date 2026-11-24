using Application.Auth;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApiRest.Contracts.Auth;

namespace WebApiRest.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public sealed class AuthController(LoginHandler loginHandler) : ControllerBase
{
    private const string ValidationCode = "VALIDATION";

    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await loginHandler.HandleAsync(
                new LoginCommand(request.Email ?? string.Empty, request.Password ?? string.Empty),
                cancellationToken);

            return Ok(new LoginResponse(result.AccessToken, "Bearer", result.ExpiresAt, result.Role.ToString()));
        }
        // Temporary: the error middleware of task 1b-06 will replace this local translation.
        catch (DomainException ex) when (ex.Code == LoginHandler.InvalidCredentialsCode)
        {
            return Failure(StatusCodes.Status401Unauthorized, "Invalid credentials", ex);
        }
        catch (DomainException ex) when (ex.Code == ValidationCode)
        {
            return Failure(StatusCodes.Status400BadRequest, "Validation error", ex);
        }
    }

    private ObjectResult Failure(int status, string title, DomainException ex)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = ex.Message };
        problem.Extensions["code"] = ex.Code;

        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
