using Application.Appointments;
using Application.Tests.Fakes;
using Domain.Appointments;
using Domain.Common;

namespace Application.Tests.Appointments;

/// <summary>Listing the agenda by clinic days, with an in-memory repository. All data is fictitious.</summary>
public class ListAppointmentsHandlerTests
{
    private static readonly TimeSpan Ecuador = TimeSpan.FromHours(-5);
    private static readonly DateOnly December1 = new(2026, 12, 1);
    private static readonly DateOnly December7 = new(2026, 12, 7);

    private readonly FakeAppointmentRepository _appointments = new();

    private ListAppointmentsHandler List =>
        new(_appointments, TestClinic.ClockAt(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero)));

    private static DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 12, day, hour, minute, 0, Ecuador);

    private AgendaItemDto Seed(DateTimeOffset start, Guid? dentistId = null, AppointmentStatus status = AppointmentStatus.Scheduled)
    {
        var item = new AgendaItemDto(
            Guid.CreateVersion7(),
            start,
            start.AddMinutes(30),
            status,
            null,
            Guid.CreateVersion7(),
            "Ana Pérez",
            dentistId ?? Guid.CreateVersion7(),
            "Carla Mora",
            Guid.CreateVersion7(),
            "Sillón 1",
            Guid.CreateVersion7(),
            "Limpieza");

        _appointments.Agenda.Add(item);
        return item;
    }

    private Task<IReadOnlyList<AgendaItemDto>> ListAsync(DateOnly from, DateOnly to, Guid? dentistId = null) =>
        List.HandleAsync(new ListAppointmentsQuery(from, to, dentistId));

    [Fact]
    public async Task List_TurnsTheDaysIntoClinicMidnights()
    {
        await ListAsync(December1, December7);

        // From December 1, 00:00 to December 8, 00:00 at the clinic (UTC-5).
        var (range, dentistId) = _appointments.LastAgendaQuery!.Value;
        Assert.Equal(new DateTimeOffset(2026, 12, 1, 5, 0, 0, TimeSpan.Zero), range.Start);
        Assert.Equal(new DateTimeOffset(2026, 12, 8, 5, 0, 0, TimeSpan.Zero), range.End);
        Assert.Null(dentistId);
    }

    [Fact]
    public async Task List_OfOneDay_CoversThatWholeClinicDay()
    {
        // 23:30 at the clinic is already the next day in UTC, but it belongs to December 7.
        var lateAtNight = Seed(At(7, 23, 30));
        Seed(At(6, 23, 30)); // Ends at 00:00 on December 7: touches the day but does not overlap it.
        Seed(At(8, 0, 0));

        var list = await ListAsync(December7, December7);

        Assert.Equal(lateAtNight.Id, Assert.Single(list).Id);
    }

    [Fact]
    public async Task List_ReturnsEveryState_OrderedByStart()
    {
        var later = Seed(At(7, 11), status: AppointmentStatus.Cancelled);
        var earlier = Seed(At(7, 9), status: AppointmentStatus.Completed);
        var middle = Seed(At(7, 10), status: AppointmentStatus.NoShow);

        var list = await ListAsync(December7, December7);

        Assert.Equal([earlier.Id, middle.Id, later.Id], list.Select(a => a.Id));
    }

    [Fact]
    public async Task List_ForADentist_PassesTheFilter()
    {
        var dentistId = Guid.CreateVersion7();
        var own = Seed(At(7, 9), dentistId);
        Seed(At(7, 9));

        var list = await ListAsync(December7, December7, dentistId);

        Assert.Equal(own.Id, Assert.Single(list).Id);
        Assert.Equal(dentistId, _appointments.LastAgendaQuery!.Value.DentistId);
    }

    [Fact]
    public async Task List_OfTheMaximumDays_IsAllowed()
    {
        await ListAsync(December1, December1.AddDays(ListAppointmentsHandler.MaxDays - 1));

        Assert.NotNull(_appointments.LastAgendaQuery);
    }

    [Fact]
    public async Task List_OfMoreThanTheMaximumDays_ThrowsValidation()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => ListAsync(December1, December1.AddDays(ListAppointmentsHandler.MaxDays)));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        Assert.Null(_appointments.LastAgendaQuery);
    }

    [Fact]
    public async Task List_WhenTheLastDayIsBeforeTheFirst_ThrowsValidation()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => ListAsync(December7, December1));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        Assert.Null(_appointments.LastAgendaQuery);
    }

    [Fact]
    public async Task List_AtTheEndOfTheCalendar_ThrowsValidation()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => ListAsync(DateOnly.MaxValue, DateOnly.MaxValue));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
    }
}
