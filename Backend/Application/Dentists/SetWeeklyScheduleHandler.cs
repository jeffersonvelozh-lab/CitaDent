using Application.Common;

namespace Application.Dentists;

/// <summary>
/// Replaces a dentist's whole weekly schedule. The domain rejects (VALIDATION) a block that ends before
/// it starts, an unknown day and blocks that overlap on the same day; then the old schedule stays.
/// Appointments already booked are not checked against the new schedule.
/// </summary>
public sealed class SetWeeklyScheduleHandler(IDentistRepository dentists, IUnitOfWork unitOfWork)
    : ICommandHandler<SetWeeklyScheduleCommand, DentistDto>
{
    public async Task<DentistDto> HandleAsync(SetWeeklyScheduleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Hours);

        var dentist = await dentists.GetByIdAsync(command.DentistId, cancellationToken)
            ?? throw DentistErrors.NotFound(command.DentistId);

        dentist.SetWeeklySchedule(command.Hours.Select(h => h.ToDomain()).ToList());

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return DentistDto.From(dentist);
    }
}
