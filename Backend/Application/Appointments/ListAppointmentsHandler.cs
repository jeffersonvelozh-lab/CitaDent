using Application.Common;
using Domain.Common;

namespace Application.Appointments;

/// <summary>
/// The agenda: the appointments that overlap some clinic days, ordered by start. The days are read in the
/// clinic's time zone: from the start of <c>From</c> to the end of <c>To</c> at the clinic. The range is limited
/// to <see cref="MaxDays"/> days, enough for a month view, so the list never grows without bound.
/// </summary>
public sealed class ListAppointmentsHandler(IAppointmentRepository appointments, ClinicClock clock)
    : IQueryHandler<ListAppointmentsQuery, IReadOnlyList<AgendaItemDto>>
{
    public const int MaxDays = 31;

    public async Task<IReadOnlyList<AgendaItemDto>> HandleAsync(
        ListAppointmentsQuery query,
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

        return await appointments.ListAgendaAsync(range, query.DentistId, cancellationToken);
    }

    // The same conversion as ListScheduleBlocksHandler. Copied on purpose for now: moving it to ClinicClock
    // would change a class other use cases rely on, which is a refactor for its own task.
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
