using Application.Chairs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApiRest.Contracts.Chairs;
using WebApiRest.Security;

namespace WebApiRest.Controllers;

/// <summary>
/// The dental chairs. Any signed-in role reads them (reception picks a chair when scheduling, the dentist
/// sees their agenda); only an admin changes them. Chairs are never deleted, only deactivated, because
/// appointments keep their id. Errors (VALIDATION, NOT-FOUND, DUPLICATE) are answered by the central
/// error handling.
/// </summary>
[ApiController]
[Route("api/chairs")]
public sealed class ChairsController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ChairDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ChairDto>> List(
        [FromServices] ListChairsHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] bool includeInactive = false) =>
        handler.HandleAsync(new ListChairsQuery(includeInactive), cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ChairDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ChairDto> Get(
        Guid id,
        [FromServices] GetChairHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new GetChairQuery(id), cancellationToken);

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType<ChairDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        ChairRequest request,
        [FromServices] CreateChairHandler handler,
        CancellationToken cancellationToken)
    {
        var created = await handler.HandleAsync(new CreateChairCommand(request.Name ?? string.Empty), cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Renames the chair; its name is the only thing a chair has to edit.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType<ChairDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ChairDto> Update(
        Guid id,
        ChairRequest request,
        [FromServices] RenameChairHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new RenameChairCommand(id, request.Name ?? string.Empty), cancellationToken);

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Activate(
        Guid id,
        [FromServices] SetChairActiveHandler handler,
        CancellationToken cancellationToken) =>
        SetActive(id, isActive: true, handler, cancellationToken);

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Deactivate(
        Guid id,
        [FromServices] SetChairActiveHandler handler,
        CancellationToken cancellationToken) =>
        SetActive(id, isActive: false, handler, cancellationToken);

    private async Task<IActionResult> SetActive(
        Guid id,
        bool isActive,
        SetChairActiveHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new SetChairActiveCommand(id, isActive), cancellationToken);
        return NoContent();
    }
}
