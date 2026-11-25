using Domain.Scheduling;

namespace Application.ScheduleBlocks;

/// <summary>
/// A schedule block as the use cases return it. <paramref name="DentistId"/> is null for a block of the whole
/// clinic; the range is in UTC and half-open [StartsAt, EndsAt).
/// </summary>
public sealed record ScheduleBlockDto(Guid Id, Guid? DentistId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Reason)
{
    public static ScheduleBlockDto From(ScheduleBlock block) =>
        new(block.Id, block.DentistId, block.Range.Start, block.Range.End, block.Reason);
}
