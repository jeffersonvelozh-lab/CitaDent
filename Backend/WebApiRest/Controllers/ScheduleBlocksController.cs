using Application.ScheduleBlocks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApiRest.Contracts.ScheduleBlocks;
using WebApiRest.Security;

namespace WebApiRest.Controllers;

/// <summary>
/// Periods when nothing new can be scheduled (RN-05): one dentist's vacation, or a holiday for the whole clinic.
/// Any signed-in role reads a block; reception and admin create and remove them. A block has no history, so it
/// is deleted for good. Errors (VALIDATION, NOT-FOUND) are answered by the central error handling.
/// </summary>
[ApiController]
[Route("api/schedule-blocks")]
public sealed class ScheduleBlocksController : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ScheduleBlockDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ScheduleBlockDto> Get(
        Guid id,
        [FromServices] GetScheduleBlockHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new GetScheduleBlockQuery(id), cancellationToken);

    /// <summary>
    /// Blocks [startsAt, endsAt), answered in UTC. A block that has already ended, or for a dentist that does not
    /// exist, is a 400; one in progress is allowed.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.StaffWithAgendaAccess)]
    [ProducesResponseType<ScheduleBlockDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        CreateScheduleBlockRequest request,
        [FromServices] CreateScheduleBlockHandler handler,
        CancellationToken cancellationToken)
    {
        // Model validation already answered 400 when an instant is missing.
        var created = await handler.HandleAsync(
            new CreateScheduleBlockCommand(
                request.DentistId,
                request.StartsAt!.Value,
                request.EndsAt!.Value,
                request.Reason ?? string.Empty),
            cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.StaffWithAgendaAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(
        Guid id,
        [FromServices] RemoveScheduleBlockHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new RemoveScheduleBlockCommand(id), cancellationToken);
        return NoContent();
    }
}
