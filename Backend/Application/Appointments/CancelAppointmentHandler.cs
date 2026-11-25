using Application.Common;
using Domain.Appointments;

namespace Application.Appointments;

/// <summary>
/// Cancels a scheduled appointment with a reason (RN-08), which frees the dentist and the chair for that time.
/// Only before it starts: once it has started it is completed or marked as a no-show instead (VALIDATION).
/// An appointment in another state gives RN-07.
/// </summary>
public sealed class CancelAppointmentHandler(IAppointmentRepository appointments, ClinicClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<CancelAppointmentCommand, AppointmentDto>
{
    public async Task<AppointmentDto> HandleAsync(CancelAppointmentCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var appointment = await appointments.GetByIdAsync(command.Id, cancellationToken)
            ?? throw AppointmentErrors.NotFound(command.Id);

        // Only for a scheduled one: for any other state the domain answers RN-07, which says more.
        if (appointment.Status == AppointmentStatus.Scheduled && appointment.Slot.Start <= clock.Now)
        {
            throw AppointmentErrors.AlreadyStarted();
        }

        appointment.Cancel(command.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return AppointmentDto.From(appointment);
    }
}
