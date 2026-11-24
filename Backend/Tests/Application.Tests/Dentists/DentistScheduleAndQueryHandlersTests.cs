using Application.Common;
using Application.Dentists;
using Application.Tests.Fakes;
using Domain.Common;
using Domain.Scheduling;

namespace Application.Tests.Dentists;

/// <summary>Set the weekly schedule, get and list dentists, with in-memory doubles. All data is fictitious.</summary>
public class DentistScheduleAndQueryHandlersTests
{
    private readonly FakeDentistRepository _dentists = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private SetWeeklyScheduleHandler SetSchedule => new(_dentists, _unitOfWork);
    private GetDentistHandler Get => new(_dentists);
    private ListDentistsHandler List => new(_dentists);

    private Dentist SeedDentist(string firstName = "Carla", string lastName = "Mora") =>
        _dentists.Seed(Dentist.Create(Guid.NewGuid(), firstName, lastName, $"LIC-{firstName}-{lastName}"));

    private static WorkingHoursDto Block(DayOfWeek day, int startHour, int endHour) =>
        new(day, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0));

    // Set weekly schedule

    [Fact]
    public async Task SetSchedule_StoresTheBlocksAndSavesOnce()
    {
        var dentist = SeedDentist();

        var result = await SetSchedule.HandleAsync(new SetWeeklyScheduleCommand(dentist.Id,
        [
            Block(DayOfWeek.Monday, 8, 12),
            Block(DayOfWeek.Monday, 14, 18)
        ]));

        Assert.Equal([Block(DayOfWeek.Monday, 8, 12), Block(DayOfWeek.Monday, 14, 18)], result.WeeklySchedule);
        Assert.Equal(2, dentist.WeeklySchedule.Count);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task SetSchedule_ReplacesTheWholeOldSchedule()
    {
        var dentist = SeedDentist();
        await SetSchedule.HandleAsync(new SetWeeklyScheduleCommand(dentist.Id,
        [
            Block(DayOfWeek.Monday, 8, 12),
            Block(DayOfWeek.Tuesday, 8, 12)
        ]));

        var result = await SetSchedule.HandleAsync(new SetWeeklyScheduleCommand(dentist.Id,
        [
            Block(DayOfWeek.Wednesday, 9, 13)
        ]));

        Assert.Equal([Block(DayOfWeek.Wednesday, 9, 13)], result.WeeklySchedule);
        Assert.Equal(DayOfWeek.Wednesday, Assert.Single(dentist.WeeklySchedule).Day);
    }

    [Fact]
    public async Task SetSchedule_WithAnEmptyList_LeavesNoWorkingHours()
    {
        var dentist = SeedDentist();
        await SetSchedule.HandleAsync(new SetWeeklyScheduleCommand(dentist.Id, [Block(DayOfWeek.Monday, 8, 12)]));

        var result = await SetSchedule.HandleAsync(new SetWeeklyScheduleCommand(dentist.Id, []));

        Assert.Empty(result.WeeklySchedule);
        Assert.Empty(dentist.WeeklySchedule);
    }

    [Fact]
    public async Task SetSchedule_WithOverlappingBlocks_ThrowsValidationAndKeepsTheOldSchedule()
    {
        var dentist = SeedDentist();
        await SetSchedule.HandleAsync(new SetWeeklyScheduleCommand(dentist.Id, [Block(DayOfWeek.Friday, 8, 12)]));

        var ex = await Assert.ThrowsAsync<DomainException>(() => SetSchedule.HandleAsync(
            new SetWeeklyScheduleCommand(dentist.Id,
            [
                Block(DayOfWeek.Monday, 8, 12),
                Block(DayOfWeek.Monday, 11, 15)
            ])));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        Assert.Equal(DayOfWeek.Friday, Assert.Single(dentist.WeeklySchedule).Day);
        Assert.Equal(1, _unitOfWork.SaveCount); // Only the first, valid schedule was saved.
    }

    [Fact]
    public async Task SetSchedule_BlocksThatTouch_AreAllowed()
    {
        var dentist = SeedDentist();

        var result = await SetSchedule.HandleAsync(new SetWeeklyScheduleCommand(dentist.Id,
        [
            Block(DayOfWeek.Monday, 8, 12),
            Block(DayOfWeek.Monday, 12, 16)
        ]));

        Assert.Equal(2, result.WeeklySchedule.Count);
    }

    [Fact]
    public async Task SetSchedule_TheSameHoursOnDifferentDays_DoNotOverlap()
    {
        var dentist = SeedDentist();

        var result = await SetSchedule.HandleAsync(new SetWeeklyScheduleCommand(dentist.Id,
        [
            Block(DayOfWeek.Monday, 8, 12),
            Block(DayOfWeek.Tuesday, 8, 12)
        ]));

        Assert.Equal(2, result.WeeklySchedule.Count);
    }

    [Theory]
    [InlineData(12, 8)]
    [InlineData(8, 8)]
    public async Task SetSchedule_WithABlockThatDoesNotEndAfterItStarts_ThrowsValidation(int start, int end)
    {
        var dentist = SeedDentist();

        var ex = await Assert.ThrowsAsync<DomainException>(() => SetSchedule.HandleAsync(
            new SetWeeklyScheduleCommand(dentist.Id, [Block(DayOfWeek.Monday, start, end)])));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task SetSchedule_WithAnUnknownDay_ThrowsValidation()
    {
        var dentist = SeedDentist();

        var ex = await Assert.ThrowsAsync<DomainException>(() => SetSchedule.HandleAsync(
            new SetWeeklyScheduleCommand(dentist.Id, [Block((DayOfWeek)7, 8, 12)])));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
    }

    [Fact]
    public async Task SetSchedule_UnknownDentist_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => SetSchedule.HandleAsync(
            new SetWeeklyScheduleCommand(Guid.NewGuid(), [Block(DayOfWeek.Monday, 8, 12)])));

        Assert.Equal(ApplicationErrorCodes.NotFound, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task SetSchedule_ReturnsTheBlocksFromMondayToSundayThenByStart()
    {
        var dentist = SeedDentist();

        var result = await SetSchedule.HandleAsync(new SetWeeklyScheduleCommand(dentist.Id,
        [
            Block(DayOfWeek.Sunday, 8, 10),
            Block(DayOfWeek.Monday, 14, 18),
            Block(DayOfWeek.Saturday, 8, 12),
            Block(DayOfWeek.Monday, 8, 12)
        ]));

        Assert.Equal(
            [
                Block(DayOfWeek.Monday, 8, 12),
                Block(DayOfWeek.Monday, 14, 18),
                Block(DayOfWeek.Saturday, 8, 12),
                Block(DayOfWeek.Sunday, 8, 10)
            ],
            result.WeeklySchedule);
    }

    // Get and list

    [Fact]
    public async Task Get_ReturnsTheDentistWithTheSchedule()
    {
        var dentist = SeedDentist();
        dentist.SetWeeklySchedule([new WorkingHours(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0))]);

        var result = await Get.HandleAsync(new GetDentistQuery(dentist.Id));

        Assert.Equal(dentist.Id, result.Id);
        Assert.Equal(dentist.UserId, result.UserId);
        Assert.Equal([Block(DayOfWeek.Monday, 8, 12)], result.WeeklySchedule);
    }

    [Fact]
    public async Task Get_UnknownId_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Get.HandleAsync(new GetDentistQuery(Guid.NewGuid())));

        Assert.Equal(ApplicationErrorCodes.NotFound, ex.Code);
    }

    [Fact]
    public async Task List_ByDefault_ReturnsOnlyActiveDentistsByLastNameThenFirstName()
    {
        var third = SeedDentist("Luis", "Vera");
        var second = SeedDentist("Rosa", "Andrade");
        var first = SeedDentist("Ana", "Andrade");
        SeedDentist("Mario", "Bravo").Deactivate();

        var result = await List.HandleAsync(new ListDentistsQuery());

        Assert.Equal([first.Id, second.Id, third.Id], result.Select(d => d.Id));
    }

    [Fact]
    public async Task List_WithInactive_IncludesThem()
    {
        SeedDentist("Luis", "Vera");
        var inactive = SeedDentist("Mario", "Bravo");
        inactive.Deactivate();

        var result = await List.HandleAsync(new ListDentistsQuery(IncludeInactive: true));

        Assert.Equal(2, result.Count);
        Assert.Contains(result, d => d.Id == inactive.Id && !d.IsActive);
    }
}
