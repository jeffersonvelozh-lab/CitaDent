using Application.Chairs;
using Application.Common;
using Application.Dentists;
using Application.Patients;
using Application.Procedures;
using Domain.Appointments;
using Domain.Common;

namespace Application.Appointments;

/// <summary>
/// Books an appointment. In order: the patient, dentist, chair and procedure must exist (VALIDATION);
/// the start cannot be in the past (RN-06; starting right now is allowed); then the domain checks RN-01 to RN-05
/// with the blocks and appointments that overlap the slot. Two requests for the same time can both pass that
/// check: the database lets only one in, and the other gets RN-02 (dentist) or RN-03 (chair).
/// </summary>
public sealed class ScheduleAppointmentHandler(
    IPatientRepository patients,
    IDentistRepository dentists,
    IChairRepository chairs,
    IProcedureRepository procedures,
    IAppointmentRepository appointments,
    SchedulingContextLoader contextLoader,
    ClinicClock clock,
    IUnitOfWork unitOfWork) : ICommandHandler<ScheduleAppointmentCommand, AppointmentDto>
{
    public async Task<AppointmentDto> HandleAsync(
        ScheduleAppointmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var patient = await patients.GetByIdAsync(command.PatientId, cancellationToken)
            ?? throw AppointmentErrors.UnknownReference("patient", command.PatientId);
        var dentist = await dentists.GetByIdAsync(command.DentistId, cancellationToken)
            ?? throw AppointmentErrors.UnknownReference("dentist", command.DentistId);
        var chair = await chairs.GetByIdAsync(command.ChairId, cancellationToken)
            ?? throw AppointmentErrors.UnknownReference("chair", command.ChairId);
        var procedure = await procedures.GetByIdAsync(command.ProcedureId, cancellationToken)
            ?? throw AppointmentErrors.UnknownReference("procedure", command.ProcedureId);

        // RN-06 belongs to Application: the domain has no clock.
        if (command.StartsAt < clock.Now)
        {
            throw AppointmentErrors.InThePast();
        }

        var slot = TimeRange.FromStartAndDuration(command.StartsAt, procedure.Duration);
        var context = await contextLoader.LoadAsync(dentist.Id, chair.Id, slot, cancellationToken);

        var appointment = Appointment.Schedule(
            patient, dentist, chair, procedure, command.StartsAt, command.CreatedByUserId, context);

        appointments.Add(appointment);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ScheduleConflictException conflict)
        {
            throw AppointmentErrors.From(conflict);
        }

        return AppointmentDto.From(appointment);
    }
}
