using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApiRest.Tests.Fakes;

/// <summary>Test-only endpoints that throw, to check the central error handling. Never in the real API.</summary>
[ApiController]
[AllowAnonymous]
[Route("test/errors")]
public sealed class ErrorProbeController : ControllerBase
{
    public const string DomainMessage = "Probe business rule message.";
    public const string SecretMessage = "Connection string with password=secreto";

    [HttpGet("domain/{code}")]
    public IActionResult Domain(string code) => throw new DomainException(code, DomainMessage);

    [HttpGet("unexpected")]
    public IActionResult Unexpected() => throw new InvalidOperationException(SecretMessage);

    [HttpPost("body")]
    public IActionResult Body(ProbeBody body) => Ok(body);

    public sealed record ProbeBody(int Number);
}
