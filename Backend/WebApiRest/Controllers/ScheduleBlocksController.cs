using System.Globalization;
using Application.ScheduleBlocks;
using Domain.Common;
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
    /// <summary>
    /// The blocks that touch the clinic days <paramref name="from"/> to <paramref name="to"/> (both included,
    /// yyyy-MM-dd, at most 366 days), ordered by start. With <paramref name="dentistId"/>, only that dentist's;
    /// without it, every dentist's. The whole-clinic blocks come too unless <paramref name="includeClinicWide"/>
    /// is false. An unknown dentist gives an empty list.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ScheduleBlockDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<IReadOnlyList<ScheduleBlockDto>> List(
        [FromServices] ListScheduleBlocksHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] Guid? dentistId = null,
        [FromQuery] bool includeClinicWide = true) =>
        handler.HandleAsync(
            new ListScheduleBlocksQuery(ParseDay(from, nameof(from)), ParseDay(to, nameof(to)), dentistId, includeClinicWide),
            cancellationToken);

    // Query binding would read "01/03/2031" as January 3 (US order), while in Ecuador it means March 1,
    // so a day is read as text and only yyyy-MM-dd is accepted, the same as DateOnly in JSON bodies.
    private static DateOnly ParseDay(string? value, string name) =>
        DateOnly.TryParseExact(value?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : throw new DomainException(DomainException.ValidationCode, $"The \"{name}\" day is required, as yyyy-MM-dd.");

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
