using Domain.Appointments;

namespace Application.Appointments;

/// <summary>
/// One appointment as the agenda shows it: the slot in UTC and half-open [StartsAt, EndsAt), with the names of
/// the patient, dentist, chair and procedure, so the screen needs no extra request per row. The names are
/// the current ones, not the ones the records had when the appointment was booked.
/// </summary>
public sealed record AgendaItemDto(
    Guid Id,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    AppointmentStatus Status,
    string? CancelReason,
    Guid PatientId,
    string PatientName,
    Guid DentistId,
    string DentistName,
    Guid ChairId,
    string ChairName,
    Guid ProcedureId,
    string ProcedureName);
