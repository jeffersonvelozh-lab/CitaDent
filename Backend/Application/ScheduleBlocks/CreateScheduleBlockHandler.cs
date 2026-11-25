using Application.Common;
using Application.Dentists;
using Domain.Common;
using Domain.Scheduling;

namespace Application.ScheduleBlocks;

/// <summary>
/// Blocks a period for one dentist or for the whole clinic, so nothing new can be scheduled in it (RN-05).
/// A block that has already ended is refused; one in progress is allowed. Blocks may overlap each other,
/// the dentist may be inactive, and appointments already scheduled in the period are kept.
/// </summary>
public sealed class CreateScheduleBlockHandler(
    IScheduleBlockRepository blocks,
    IDentistRepository dentists,
    ClinicClock clock,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateScheduleBlockCommand, ScheduleBlockDto>
{
    public async Task<ScheduleBlockDto> HandleAsync(
        CreateScheduleBlockCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Checked here, so a reversed range is malformed input (VALIDATION) and not RN-01 of an appointment.
        if (command.EndsAt <= command.StartsAt)
        {
            throw ScheduleBlockErrors.EndBeforeStart();
        }

        // Domain next: it validates the reason and an empty dentist id (VALIDATION).
        var block = ScheduleBlock.Create(command.DentistId, new TimeRange(command.StartsAt, command.EndsAt), command.Reason);

        if (block.Range.End <= clock.Now)
        {
            throw ScheduleBlockErrors.AlreadyEnded();
        }

        if (block.DentistId is { } dentistId
            && await dentists.GetByIdAsync(dentistId, cancellationToken) is null)
        {
            throw ScheduleBlockErrors.UnknownDentist(dentistId);
        }

        blocks.Add(block);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ScheduleBlockDto.From(block);
    }
}
