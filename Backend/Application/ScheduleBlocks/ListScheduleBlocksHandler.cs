using Application.Common;
using Domain.Common;

namespace Application.ScheduleBlocks;

/// <summary>
/// The schedule blocks that overlap some clinic days, ordered by start. The days are read in the clinic's
/// time zone: from the start of <c>From</c> to the end of <c>To</c> at the clinic. The range is limited to
/// <see cref="MaxDays"/> days, so the list never grows without bound.
/// </summary>
public sealed class ListScheduleBlocksHandler(IScheduleBlockRepository blocks, ClinicClock clock)
    : IQueryHandler<ListScheduleBlocksQuery, IReadOnlyList<ScheduleBlockDto>>
{
    public const int MaxDays = 366;

    public async Task<IReadOnlyList<ScheduleBlockDto>> HandleAsync(
        ListScheduleBlocksQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.To < query.From)
        {
            throw new DomainException(DomainException.ValidationCode, "The last day must not be before the first one.");
        }

        if (query.To.DayNumber - query.From.DayNumber + 1 > MaxDays)
        {
            throw new DomainException(DomainException.ValidationCode, $"The range can span at most {MaxDays} days.");
        }

        var range = new TimeRange(StartOfDay(query.From), StartOfDay(NextDay(query.To)));

        var list = await blocks.ListAsync(range, query.DentistId, query.IncludeClinicWide, cancellationToken);

        return list.Select(ScheduleBlockDto.From).ToList();
    }

    private static DateOnly NextDay(DateOnly day) =>
        day < DateOnly.MaxValue
            ? day.AddDays(1)
            : throw new DomainException(DomainException.ValidationCode, "The last day is out of range.");

    // Midnight at the clinic, as an instant. Fails with VALIDATION at the edges of the calendar.
    private DateTimeOffset StartOfDay(DateOnly day)
    {
        var localMidnight = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        try
        {
            return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMidnight, clock.TimeZone), TimeSpan.Zero);
        }
        catch (ArgumentException)
        {
            throw new DomainException(DomainException.ValidationCode, "The day is out of range.");
        }
    }
}
