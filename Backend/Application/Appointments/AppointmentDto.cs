using Domain.Appointments;

namespace Application.Appointments;

/// <summary>An appointment as the use cases return it. The slot is in UTC and half-open [StartsAt, EndsAt).</summary>
public sealed record AppointmentDto(
    Guid Id,
    Guid PatientId,
    Guid DentistId,
    Guid ChairId,
    Guid ProcedureId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    AppointmentStatus Status,
    string? CancelReason,
    Guid CreatedByUserId)
{
    public static AppointmentDto From(Appointment appointment) =>
        new(
            appointment.Id,
            appointment.PatientId,
            appointment.DentistId,
            appointment.ChairId,
            appointment.ProcedureId,
            appointment.Slot.Start,
            appointment.Slot.End,
            appointment.Status,
            appointment.CancelReason,
            appointment.CreatedByUserId);
}
