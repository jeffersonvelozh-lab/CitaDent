using Application.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApiRest.Contracts.Appointments;
using WebApiRest.Security;

namespace WebApiRest.Controllers;

/// <summary>
/// Appointments. Reception and admin book them; any signed-in role reads one. Errors are answered by the central
/// error handling: VALIDATION (400), NOT-FOUND (404), RN-02, RN-03 and RN-05 (409), RN-06 (422).
/// </summary>
[ApiController]
[Route("api/appointments")]
public sealed class AppointmentsController : ControllerBase
{
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
}
