using Application.Common;
using Domain.Common;

namespace Application.ScheduleBlocks;

internal static class ScheduleBlockErrors
{
    public static DomainException NotFound(Guid id) =>
        new(ApplicationErrorCodes.NotFound, $"Schedule block {id} was not found.");

    public static DomainException EndBeforeStart() =>
        new(DomainException.ValidationCode, "The end of a schedule block must be after its start.");

    public static DomainException AlreadyEnded() =>
        new(DomainException.ValidationCode, "The schedule block has already ended.");

    public static DomainException UnknownDentist(Guid dentistId) =>
        new(DomainException.ValidationCode, $"Dentist {dentistId} does not exist.");
}
