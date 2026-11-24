using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApiRest.Security;

namespace WebApiRest.Tests.Fakes;

/// <summary>
/// Test-only endpoints, one per policy. The factory adds this controller to the app;
/// it never exists in the real API.
/// </summary>
[ApiController]
[Route("test/authorization")]
public sealed class AuthorizationProbeController : ControllerBase
{
    // No attribute: protected only by the fallback policy (any valid token).
    [HttpGet("authenticated")]
    public IActionResult Authenticated() => Ok();

    [HttpGet("admin")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public IActionResult Admin() => Ok();

    [HttpGet("agenda")]
    [Authorize(Policy = AuthorizationPolicies.StaffWithAgendaAccess)]
    public IActionResult Agenda() => Ok();

    [HttpGet("clinical")]
    [Authorize(Policy = AuthorizationPolicies.ClinicalAccess)]
    public IActionResult Clinical() => Ok();
}
