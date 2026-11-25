using Application.ScheduleBlocks;
using Application.Tests.Fakes;
using Domain.Common;
using Domain.Scheduling;

namespace Application.Tests.ScheduleBlocks;

/// <summary>Listing schedule blocks by clinic days, with an in-memory repository.</summary>
public class ListScheduleBlocksHandlerTests
{
    private static readonly TimeSpan Ecuador = TimeSpan.FromHours(-5);
    private static readonly DateOnly December1 = new(2026, 12, 1);
    private static readonly DateOnly December31 = new(2026, 12, 31);

    private readonly FakeScheduleBlockRepository _blocks = new();

    private ListScheduleBlocksHandler List =>
        new(_blocks, TestClinic.ClockAt(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero)));

    private static DateTimeOffset At(int month, int day, int hour) => new(2026, month, day, hour, 0, 0, Ecuador);

    private ScheduleBlock Seed(Guid? dentistId, DateTimeOffset start, DateTimeOffset end) =>
        _blocks.Seed(ScheduleBlock.Create(dentistId, new TimeRange(start, end), "Bloqueo"));

    private Task<IReadOnlyList<ScheduleBlockDto>> ListAsync(
        DateOnly from,
        DateOnly to,
        Guid? dentistId = null,
        bool includeClinicWide = true) =>
        List.HandleAsync(new ListScheduleBlocksQuery(from, to, dentistId, includeClinicWide));

    [Fact]
    public async Task List_TurnsTheDaysIntoClinicMidnights()
    {
        await ListAsync(December1, December31);

        // From December 1, 00:00 to January 1, 00:00 at the clinic (UTC-5).
        Assert.Equal(new DateTimeOffset(2026, 12, 1, 5, 0, 0, TimeSpan.Zero), _blocks.LastListedRange!.Start);
        Assert.Equal(new DateTimeOffset(2027, 1, 1, 5, 0, 0, TimeSpan.Zero), _blocks.LastListedRange.End);
    }

    [Fact]
    public async Task List_OfOneDay_CoversThatWholeDay()
    {
        var lateAtNight = Seed(null, At(12, 24, 23), At(12, 25, 1));
        Seed(null, At(12, 23, 20), At(12, 24, 0)); // Ends when December 24 starts.

        var list = await ListAsync(new DateOnly(2026, 12, 24), new DateOnly(2026, 12, 24));

        Assert.Equal(lateAtNight.Id, Assert.Single(list).Id);
    }

    [Fact]
    public async Task List_ReturnsTheBlocksOrderedByStart()
    {
        var second = Seed(null, At(12, 24, 8), At(12, 24, 18));
        var first = Seed(null, At(12, 2, 8), At(12, 2, 12));

        var list = await ListAsync(December1, December31);

        Assert.Equal([first.Id, second.Id], list.Select(b => b.Id));
    }

    [Fact]
    public async Task List_ForADentist_PassesTheFilters()
    {
        var dentistId = Guid.CreateVersion7();
        var own = Seed(dentistId, At(12, 2, 8), At(12, 2, 12));
        Seed(Guid.CreateVersion7(), At(12, 3, 8), At(12, 3, 12));
        var clinic = Seed(null, At(12, 24, 8), At(12, 24, 18));

        var withClinic = await ListAsync(December1, December31, dentistId);
        var withoutClinic = await ListAsync(December1, December31, dentistId, includeClinicWide: false);

        Assert.Equal([own.Id, clinic.Id], withClinic.Select(b => b.Id));
        Assert.Equal([own.Id], withoutClinic.Select(b => b.Id));
    }

    [Fact]
    public async Task List_OfASingleDay_IsAllowed()
    {
        var list = await ListAsync(December1, December1);

        Assert.Empty(list);
    }

    [Fact]
    public async Task List_WhenTheLastDayIsBeforeTheFirst_ThrowsValidation()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => ListAsync(December31, December1));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        Assert.Null(_blocks.LastListedRange);
    }

    [Fact]
    public async Task List_OfTheMaximumDays_IsAllowed()
    {
        await ListAsync(December1, December1.AddDays(ListScheduleBlocksHandler.MaxDays - 1));

        Assert.NotNull(_blocks.LastListedRange);
    }

    [Fact]
    public async Task List_OfMoreThanTheMaximumDays_ThrowsValidation()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => ListAsync(December1, December1.AddDays(ListScheduleBlocksHandler.MaxDays)));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        Assert.Null(_blocks.LastListedRange);
    }

    [Fact]
    public async Task List_AtTheEndOfTheCalendar_ThrowsValidation()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => ListAsync(DateOnly.MaxValue, DateOnly.MaxValue));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
    }
}
