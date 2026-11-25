using System.ComponentModel.DataAnnotations;

namespace WebApiRest.Contracts.Appointments;

/// <summary>
/// Body of POST /api/appointments:
/// <c>{ "patientId": "...", "dentistId": "...", "chairId": "...", "procedureId": "...", "startsAt": "2026-12-07T09:00:00-05:00" }</c>.
/// There is no end: the appointment lasts what the procedure lasts (RN-01). The user who books it comes from the
/// token, never from the body. Every field is required because a missing Guid would otherwise arrive as empty.
/// </summary>
public sealed record ScheduleAppointmentRequest(
    [Required] Guid? PatientId,
    [Required] Guid? DentistId,
    [Required] Guid? ChairId,
    [Required] Guid? ProcedureId,
    [Required] DateTimeOffset? StartsAt);
