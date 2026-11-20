using Domain.Common;
using Domain.Scheduling;
using Xunit;

namespace Domain.Tests.Scheduling;

public class DentistTests
{
    // Ecuador has no daylight saving time, so a fixed UTC-5 zone behaves the same on any machine.
    private static readonly TimeZoneInfo Ecuador =
        TimeZoneInfo.CreateCustomTimeZone("ecuador-test", TimeSpan.FromHours(-5), "Ecuador", "Ecuador");

    private static readonly TimeSpan EcuadorOffset = TimeSpan.FromHours(-5);

    // December 7, 2026 is a Monday and December 6 is a Sunday.
    private static TimeRange LocalSlot(int day, int hour, int minute, int durationMinutes) =>
        TimeRange.FromStartAndDuration(
            new DateTimeOffset(2026, 12, day, hour, minute, 0, EcuadorOffset),
            TimeSpan.FromMinutes(durationMinutes));

    private static Dentist DentistWithMondaySchedule()
    {
        var dentist = Dentist.Create(Guid.NewGuid(), "Carla", "Mora", "LIC-123", "Orthodontics");

        dentist.SetWeeklySchedule(
        [
            new WorkingHours(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            new WorkingHours(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(18, 0))
        ]);

        return dentist;
    }

    [Fact]
    public void Create_ReturnsAnActiveDentist()
    {
        var userId = Guid.NewGuid();

        var dentist = Dentist.Create(userId, " Carla ", "Mora", "LIC-123");

        Assert.NotEqual(Guid.Empty, dentist.Id);
        Assert.Equal(userId, dentist.UserId);
        Assert.Equal("Carla", dentist.FirstName);
        Assert.Null(dentist.Specialty);
        Assert.True(dentist.IsActive);
        Assert.Empty(dentist.WeeklySchedule);
    }

    [Fact]
    public void Create_Throws_WhenTheUserIdIsEmpty()
    {
        var ex = Assert.Throws<DomainException>(() => Dentist.Create(Guid.Empty, "Carla", "Mora", "LIC-123"));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Throws_WhenTheLicenseNumberIsBlank(string? license)
    {
        var ex = Assert.Throws<DomainException>(() => Dentist.Create(Guid.NewGuid(), "Carla", "Mora", license!));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Fact]
    public void WorkingHours_Throws_WhenTheEndIsNotAfterTheStart()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new WorkingHours(DayOfWeek.Monday, new TimeOnly(12, 0), new TimeOnly(12, 0)));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Fact]
    public void SetWeeklySchedule_Throws_WhenBlocksOnTheSameDayOverlap()
    {
        var dentist = Dentist.Create(Guid.NewGuid(), "Carla", "Mora", "LIC-123");

        var ex = Assert.Throws<DomainException>(() => dentist.SetWeeklySchedule(
        [
            new WorkingHours(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            new WorkingHours(DayOfWeek.Monday, new TimeOnly(11, 0), new TimeOnly(15, 0))
        ]));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Fact]
    public void SetWeeklySchedule_AllowsBlocksThatTouch()
    {
        var dentist = Dentist.Create(Guid.NewGuid(), "Carla", "Mora", "LIC-123");

        dentist.SetWeeklySchedule(
        [
            new WorkingHours(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            new WorkingHours(DayOfWeek.Monday, new TimeOnly(12, 0), new TimeOnly(14, 0))
        ]);

        Assert.Equal(2, dentist.WeeklySchedule.Count);
    }

    [Fact]
    public void SetWeeklySchedule_ReplacesThePreviousSchedule()
    {
        var dentist = DentistWithMondaySchedule();

        dentist.SetWeeklySchedule([new WorkingHours(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(13, 0))]);

        var only = Assert.Single(dentist.WeeklySchedule);
        Assert.Equal(DayOfWeek.Tuesday, only.Day);
    }

    [Fact]
    public void SetWeeklySchedule_KeepsTheOldSchedule_WhenTheNewOneIsInvalid()
    {
        var dentist = DentistWithMondaySchedule();

        Assert.Throws<DomainException>(() => dentist.SetWeeklySchedule(
        [
            new WorkingHours(DayOfWeek.Friday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            new WorkingHours(DayOfWeek.Friday, new TimeOnly(10, 0), new TimeOnly(14, 0))
        ]));

        Assert.Equal(2, dentist.WeeklySchedule.Count);
        Assert.All(dentist.WeeklySchedule, h => Assert.Equal(DayOfWeek.Monday, h.Day));
    }

    [Theory]
    [InlineData(7, 9, 0, 30, true)]    // Monday inside the morning block
    [InlineData(7, 8, 0, 240, true)]   // exactly the whole morning block
    [InlineData(7, 14, 0, 60, true)]   // start of the afternoon block
    [InlineData(7, 11, 30, 60, false)] // starts in the morning, ends after it
    [InlineData(7, 12, 0, 60, false)]  // lunch break
    [InlineData(7, 13, 30, 60, false)] // starts before the afternoon block
    [InlineData(7, 7, 30, 60, false)]  // starts before opening
    [InlineData(7, 17, 30, 60, false)] // ends after closing
    [InlineData(6, 9, 0, 30, false)]   // Sunday, no working hours
    public void WorksDuring_ChecksThatTheWholeSlotFitsInOneBlock(
        int day, int hour, int minute, int durationMinutes, bool expected)
    {
        var dentist = DentistWithMondaySchedule();

        Assert.Equal(expected, dentist.WorksDuring(LocalSlot(day, hour, minute, durationMinutes), Ecuador));
    }

    [Fact]
    public void WorksDuring_JudgesTheSlotInTheClinicTimeZone()
    {
        var dentist = DentistWithMondaySchedule();

        // 13:00 UTC is 08:00 in Ecuador, inside Monday's morning block.
        // Read as UTC without conversion it would fall in the lunch break.
        var slot = TimeRange.FromStartAndDuration(
            new DateTimeOffset(2026, 12, 7, 13, 0, 0, TimeSpan.Zero),
            TimeSpan.FromMinutes(30));

        Assert.True(dentist.WorksDuring(slot, Ecuador));
    }

    [Fact]
    public void WorksDuring_ReturnsFalse_WhenThereIsNoSchedule()
    {
        var dentist = Dentist.Create(Guid.NewGuid(), "Carla", "Mora", "LIC-123");

        Assert.False(dentist.WorksDuring(LocalSlot(7, 9, 0, 30), Ecuador));
    }

    [Fact]
    public void DeactivateAndActivate_ToggleTheState()
    {
        var dentist = DentistWithMondaySchedule();

        dentist.Deactivate();
        Assert.False(dentist.IsActive);

        dentist.Activate();
        Assert.True(dentist.IsActive);
    }
}