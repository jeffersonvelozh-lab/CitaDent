using Domain.Common;
using Domain.Scheduling;
using Xunit;

namespace Domain.Tests.Scheduling;

public class ScheduleBlockTests
{
    private static readonly DateTimeOffset T0 = new(2026, 12, 7, 9, 0, 0, TimeSpan.Zero);

    private static TimeRange Range(int startMinutes, int endMinutes) =>
        new(T0.AddMinutes(startMinutes), T0.AddMinutes(endMinutes));

    [Fact]
    public void Create_ReturnsABlock_ForADentist()
    {
        var dentistId = Guid.NewGuid();

        var block = ScheduleBlock.Create(dentistId, Range(0, 60), "  Vacation  ");

        Assert.NotEqual(Guid.Empty, block.Id);
        Assert.Equal(dentistId, block.DentistId);
        Assert.Equal("Vacation", block.Reason);
    }

    [Fact]
    public void Create_AllowsAClinicWideBlock()
    {
        var block = ScheduleBlock.Create(null, Range(0, 60), "Holiday");

        Assert.Null(block.DentistId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Throws_WhenTheReasonIsBlank(string? reason)
    {
        var ex = Assert.Throws<DomainException>(() => ScheduleBlock.Create(null, Range(0, 60), reason!));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Fact]
    public void Create_Throws_WhenTheDentistIdIsEmpty()
    {
        var ex = Assert.Throws<DomainException>(() => ScheduleBlock.Create(Guid.Empty, Range(0, 60), "Vacation"));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Fact]
    public void Affects_ReturnsTrue_OnlyForTheBlockedDentist()
    {
        var blocked = Guid.NewGuid();
        var other = Guid.NewGuid();
        var block = ScheduleBlock.Create(blocked, Range(0, 60), "Vacation");

        Assert.True(block.Affects(blocked, Range(30, 90)));
        Assert.False(block.Affects(other, Range(30, 90)));
    }

    [Fact]
    public void Affects_ReturnsTrue_ForEveryDentist_WhenTheBlockIsClinicWide()
    {
        var block = ScheduleBlock.Create(null, Range(0, 60), "Holiday");

        Assert.True(block.Affects(Guid.NewGuid(), Range(30, 90)));
        Assert.True(block.Affects(Guid.NewGuid(), Range(0, 60)));
    }

    [Theory]
    [InlineData(60, 90)]  // starts exactly when the block ends
    [InlineData(-30, 0)]  // ends exactly when the block starts
    [InlineData(120, 150)] // far away
    public void Affects_ReturnsFalse_WhenTheSlotDoesNotOverlap(int slotStart, int slotEnd)
    {
        var dentistId = Guid.NewGuid();
        var block = ScheduleBlock.Create(dentistId, Range(0, 60), "Vacation");

        Assert.False(block.Affects(dentistId, Range(slotStart, slotEnd)));
    }
}