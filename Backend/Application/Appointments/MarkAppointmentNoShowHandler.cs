using Application.Common;
using Domain.Appointments;

namespace Application.Appointments;

/// <summary>
/// Marks that the patient did not come. Only once the appointment has started (VALIDATION before); an appointment
/// that is not scheduled gives RN-07.
/// </summary>
public sealed class MarkAppointmentNoShowHandler(IAppointmentRepository appointments, ClinicClock clock, IUnitOfWork unitOfWork)
    : ICommandHandler<MarkAppointmentNoShowCommand, AppointmentDto>
{
    public async Task<AppointmentDto> HandleAsync(MarkAppointmentNoShowCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var appointment = await appointments.GetByIdAsync(command.Id, cancellationToken)
            ?? throw AppointmentErrors.NotFound(command.Id);

        // Only for a scheduled one: for any other state the domain answers RN-07, which says more.
        if (appointment.Status == AppointmentStatus.Scheduled && clock.Now < appointment.Slot.Start)
        {
            throw AppointmentErrors.NotStartedYet("marked as a no-show");
        }

        appointment.MarkNoShow();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return AppointmentDto.From(appointment);
    }
}
