using Application.Dentists;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApiRest.Contracts.Dentists;
using WebApiRest.Security;
using WebApiRest.Validation;

namespace WebApiRest.Controllers;

/// <summary>
/// Dentists and their weekly schedules. Any signed-in role reads them (reception books with them, a dentist
/// sees their schedule); only an admin registers a dentist, with the user account they sign in with, edits
/// their data and replaces a schedule. Errors (VALIDATION, NOT-FOUND, DUPLICATE) are answered by the central error handling.
/// </summary>
[ApiController]
[Route("api/dentists")]
public sealed class DentistsController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DentistDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<DentistDto>> List(
        [FromServices] ListDentistsHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] bool includeInactive = false) =>
        handler.HandleAsync(new ListDentistsQuery(includeInactive), cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DentistDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<DentistDto> Get(
        Guid id,
        [FromServices] GetDentistHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new GetDentistQuery(id), cancellationToken);

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType<DentistDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        RegisterDentistRequest request,
        [FromServices] RegisterDentistHandler handler,
        CancellationToken cancellationToken)
    {
        var created = await handler.HandleAsync(
            new RegisterDentistCommand(
                request.Email ?? string.Empty,
                request.Password ?? string.Empty,
                request.FirstName ?? string.Empty,
                request.LastName ?? string.Empty,
                request.LicenseNumber ?? string.Empty,
                request.Specialty),
            cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>
    /// Replaces the dentist's personal data and updates the full name of their user account. The license
    /// is unique ignoring case (409); a missing or empty specialty clears it.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType<DentistDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<DentistDto> Update(
        Guid id,
        UpdateDentistRequest request,
        [FromServices] UpdateDentistHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(
            new UpdateDentistCommand(
                id,
                request.FirstName ?? string.Empty,
                request.LastName ?? string.Empty,
                request.LicenseNumber ?? string.Empty,
                request.Specialty),
            cancellationToken);

    /// <summary>
    /// Replaces the whole weekly schedule with the blocks in the body; an empty array leaves none.
    /// Blocks that overlap on the same day are rejected (VALIDATION) and the old schedule stays.
    /// </summary>
    [HttpPut("{id:guid}/schedule")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType<DentistDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<DentistDto> SetWeeklySchedule(
        Guid id,
        [NoNullItems] IReadOnlyList<WorkingHoursRequest> hours,
        [FromServices] SetWeeklyScheduleHandler handler,
        CancellationToken cancellationToken) =>
        // Model validation already answered 400 for "[null]" and for a block without day, start or end.
        handler.HandleAsync(
            new SetWeeklyScheduleCommand(
                id,
                hours.Select(h => new WorkingHoursDto(h.Day!.Value, h.Start!.Value, h.End!.Value)).ToList()),
            cancellationToken);
}
