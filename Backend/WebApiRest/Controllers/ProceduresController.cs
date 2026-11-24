using Application.Procedures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApiRest.Contracts.Procedures;
using WebApiRest.Security;

namespace WebApiRest.Controllers;

/// <summary>
/// The procedure catalog. Any signed-in role reads it (reception and dentists pick procedures when
/// scheduling); only an admin changes it. Procedures are never deleted, only deactivated.
/// Errors (VALIDATION, RN-16, NOT-FOUND, DUPLICATE) are answered by the central error handling.
/// </summary>
[ApiController]
[Route("api/procedures")]
public sealed class ProceduresController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProcedureDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ProcedureDto>> List(
        [FromServices] ListProceduresHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] bool includeInactive = false) =>
        handler.HandleAsync(new ListProceduresQuery(includeInactive), cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProcedureDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ProcedureDto> Get(
        Guid id,
        [FromServices] GetProcedureHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new GetProcedureQuery(id), cancellationToken);

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType<ProcedureDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        ProcedureRequest request,
        [FromServices] CreateProcedureHandler handler,
        CancellationToken cancellationToken)
    {
        var created = await handler.HandleAsync(
            new CreateProcedureCommand(
                request.Name ?? string.Empty, request.Description, request.DurationMinutes, request.BasePrice),
            cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType<ProcedureDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<ProcedureDto> Update(
        Guid id,
        ProcedureRequest request,
        [FromServices] UpdateProcedureHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(
            new UpdateProcedureCommand(
                id, request.Name ?? string.Empty, request.Description, request.DurationMinutes, request.BasePrice),
            cancellationToken);

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Activate(
        Guid id,
        [FromServices] SetProcedureActiveHandler handler,
        CancellationToken cancellationToken) =>
        SetActive(id, isActive: true, handler, cancellationToken);

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Deactivate(
        Guid id,
        [FromServices] SetProcedureActiveHandler handler,
        CancellationToken cancellationToken) =>
        SetActive(id, isActive: false, handler, cancellationToken);

    private async Task<IActionResult> SetActive(
        Guid id,
        bool isActive,
        SetProcedureActiveHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new SetProcedureActiveCommand(id, isActive), cancellationToken);
        return NoContent();
    }
}
