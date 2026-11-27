using System.Globalization;
using Application.Appointments;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApiRest.Contracts.Appointments;
using WebApiRest.Security;

namespace WebApiRest.Controllers;

/// <summary>
/// Appointments. Reception and admin book, cancel and mark no-shows; an admin or the assigned dentist completes
/// them (RN-09); any signed-in role reads one. Errors are answered by the central
/// error handling: VALIDATION (400), RN-09 (403), NOT-FOUND (404), RN-02, RN-03, RN-05 and RN-07 (409),
/// RN-06 and RN-08 (422).
/// </summary>
[ApiController]
[Route("api/appointments")]
public sealed class AppointmentsController : ControllerBase
{
    /// <summary>
    /// The agenda: the appointments that touch the clinic days <paramref name="from"/> to <paramref name="to"/>
    /// (both included, yyyy-MM-dd, at most 31 days), in every state, ordered by start, with the names of the
    /// patient, dentist, chair and procedure. With <paramref name="dentistId"/>, only that dentist's; an unknown
    /// dentist gives an empty list. Any signed-in role.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AgendaItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<IReadOnlyList<AgendaItemDto>> List(
        [FromServices] ListAppointmentsHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] Guid? dentistId = null) =>
        handler.HandleAsync(
            new ListAppointmentsQuery(ParseDay(from, nameof(from)), ParseDay(to, nameof(to)), dentistId),
            cancellationToken);

    // Only yyyy-MM-dd, as in ScheduleBlocksController: query binding would read "01/03/2031" in US order.
    private static DateOnly ParseDay(string? value, string name) =>
        DateOnly.TryParseExact(value?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : throw new DomainException(DomainException.ValidationCode, $"The \"{name}\" day is required, as yyyy-MM-dd.");

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<AppointmentDto> Get(
        Guid id,
        [FromServices] GetAppointmentHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new GetAppointmentQuery(id), cancellationToken);

    /// <summary>
    /// Books the procedure for the patient with the dentist and chair at startsAt (with an offset). The end is the
    /// start plus the procedure duration, and the signed-in user is kept as the one who booked it.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.StaffWithAgendaAccess)]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Schedule(
        ScheduleAppointmentRequest request,
        [FromServices] ScheduleAppointmentHandler handler,
        CancellationToken cancellationToken)
    {
        // Model validation already answered 400 when a field is missing.
        var created = await handler.HandleAsync(
            new ScheduleAppointmentCommand(
                request.PatientId!.Value,
                request.DentistId!.Value,
                request.ChairId!.Value,
                request.ProcedureId!.Value,
                request.StartsAt!.Value,
                User.GetUserId()),
            cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>
    /// Cancels the appointment with a reason (RN-08, 422 without one) and frees its time. Only before it starts (400);
    /// one that is no longer scheduled gives RN-07 (409).
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = AuthorizationPolicies.StaffWithAgendaAccess)]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<AppointmentDto> Cancel(
        Guid id,
        CancelAppointmentRequest request,
        [FromServices] CancelAppointmentHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new CancelAppointmentCommand(id, request.Reason ?? string.Empty), cancellationToken);

    /// <summary>The patient did not come. Only once the appointment has started (400); RN-07 (409) if not scheduled.</summary>
    [HttpPost("{id:guid}/no-show")]
    [Authorize(Policy = AuthorizationPolicies.StaffWithAgendaAccess)]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<AppointmentDto> MarkNoShow(
        Guid id,
        [FromServices] MarkAppointmentNoShowHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new MarkAppointmentNoShowCommand(id), cancellationToken);

    /// <summary>
    /// Marks the appointment as done: an admin, or the dentist assigned to it (RN-09, 403 for another dentist).
    /// Only once it has started (400); RN-07 (409) if not scheduled.
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = AuthorizationPolicies.ClinicalAccess)]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<AppointmentDto> Complete(
        Guid id,
        [FromServices] CompleteAppointmentHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new CompleteAppointmentCommand(id, User.GetUserId(), User.GetUserRole()), cancellationToken);
}
