namespace Application.Appointments;

/// <summary>
/// Books <paramref name="ProcedureId"/> for the patient with the dentist and the chair, starting at
/// <paramref name="StartsAt"/>; it lasts what the procedure lasts (RN-01). <paramref name="CreatedByUserId"/>
/// is the signed-in user.
/// </summary>
public sealed record ScheduleAppointmentCommand(
    Guid PatientId,
    Guid DentistId,
    Guid ChairId,
    Guid ProcedureId,
    DateTimeOffset StartsAt,
    Guid CreatedByUserId);
