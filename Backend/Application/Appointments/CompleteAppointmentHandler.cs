using Application.Common;
using Application.Dentists;
using Domain.Appointments;
using Domain.Users;

namespace Application.Appointments;

/// <summary>
/// Marks an appointment as completed. RN-09: only an admin or the assigned dentist (the dentist whose user account is
/// the signed-in user) may do it. Then, only once it has started (VALIDATION before); an appointment that is not
/// scheduled gives RN-07.
/// </summary>
public sealed class CompleteAppointmentHandler(
    IAppointmentRepository appointments,
    IDentistRepository dentists,
    ClinicClock clock,
    IUnitOfWork unitOfWork) : ICommandHandler<CompleteAppointmentCommand, AppointmentDto>
{
    public async Task<AppointmentDto> HandleAsync(CompleteAppointmentCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var appointment = await appointments.GetByIdAsync(command.Id, cancellationToken)
            ?? throw AppointmentErrors.NotFound(command.Id);

        if (command.ActorRole != UserRole.Admin && !await IsTheAssignedDentistAsync(appointment, command, cancellationToken))
        {
            throw AppointmentErrors.NotTheAssignedDentist();
        }

        // Only for a scheduled one: for any other state the domain answers RN-07, which says more.
        if (appointment.Status == AppointmentStatus.Scheduled && clock.Now < appointment.Slot.Start)
        {
            throw AppointmentErrors.NotStartedYet("completed");
        }

        appointment.Complete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return AppointmentDto.From(appointment);
    }

    private async Task<bool> IsTheAssignedDentistAsync(
        Appointment appointment,
        CompleteAppointmentCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ActorRole != UserRole.Dentist)
        {
            return false;
        }

        var dentist = await dentists.GetByIdAsync(appointment.DentistId, cancellationToken);
        return dentist?.UserId == command.ActorUserId;
    }
}
