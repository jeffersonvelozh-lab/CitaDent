using Domain.Appointments;
using Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Infrastructure.Tests.TestData;

namespace Infrastructure.Tests;

/// <summary>
/// Checks that what the domain creates is saved and loaded back correctly by the EF Core mapping.
/// </summary>
public class PersistenceMappingTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public void ADentist_IsSavedAndLoaded_WithItsWeeklySchedule()
    {
        var dentist = NewDentist();
        fixture.Save(dentist);

        using var context = fixture.CreateContext();
        var loaded = context.Dentists.Single(d => d.Id == dentist.Id);

        Assert.Equal(2, loaded.WeeklySchedule.Count);
        Assert.Contains(
            loaded.WeeklySchedule,
            h => h.Day == DayOfWeek.Monday && h.Start == new TimeOnly(8, 0) && h.End == new TimeOnly(12, 0));
        Assert.Contains(
            loaded.WeeklySchedule,
            h => h.Day == DayOfWeek.Monday && h.Start == new TimeOnly(14, 0) && h.End == new TimeOnly(18, 0));
    }

    [Fact]
    public void ReplacingTheWeeklySchedule_ReplacesTheRowsInTheDatabase()
    {
        var dentist = NewDentist();
        fixture.Save(dentist);

        using (var context = fixture.CreateContext())
        {
            var loaded = context.Dentists.Single(d => d.Id == dentist.Id);
            loaded.SetWeeklySchedule([new WorkingHours(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(13, 0))]);
            context.SaveChanges();
        }

        using var check = fixture.CreateContext();
        var reloaded = check.Dentists.Single(d => d.Id == dentist.Id);
        var only = Assert.Single(reloaded.WeeklySchedule);
        Assert.Equal(DayOfWeek.Tuesday, only.Day);
        Assert.Equal(new TimeOnly(9, 0), only.Start);
    }

    [Fact]
    public void AnAppointment_KeepsItsSlotAndStatus_WhenSavedAndLoaded()
    {
        var dentist = NewDentist();
        var chair = NewChair();
        var appointment = ScheduleBlindly(fixture, dentist, chair, MondayAt(9, 0));
        fixture.Save(dentist, chair, appointment);

        using var context = fixture.CreateContext();
        var loaded = context.Appointments.Single(a => a.Id == appointment.Id);

        Assert.Equal(appointment.Slot.Start, loaded.Slot.Start);
        Assert.Equal(appointment.Slot.End, loaded.Slot.End);
        Assert.Equal(AppointmentStatus.Scheduled, loaded.Status);
        Assert.Equal(fixture.SeedPatient.Id, loaded.PatientId);
    }

    [Fact]
    public void ABlockForTheWholeClinic_IsSavedWithoutADentist()
    {
        var block = ScheduleBlock.Create(null, new Domain.Common.TimeRange(MondayAt(8, 0), MondayAt(18, 0)), "Holiday");
        fixture.Save(block);

        using var context = fixture.CreateContext();
        var loaded = context.ScheduleBlocks.Single(b => b.Id == block.Id);

        Assert.Null(loaded.DentistId);
        Assert.Equal(block.Range.Start, loaded.Range.Start);
        Assert.Equal("Holiday", loaded.Reason);
    }

    [Fact]
    public void SavingAndUpdating_FillsCreatedAtAndUpdatedAt()
    {
        var chair = NewChair();
        fixture.Save(chair);

        DateTimeOffset createdAtFirst;

        using (var context = fixture.CreateContext())
        {
            var loaded = context.Chairs.Single(c => c.Id == chair.Id);
            createdAtFirst = context.Entry(loaded).Property<DateTimeOffset>("CreatedAt").CurrentValue;

            Assert.True(createdAtFirst > DateTimeOffset.UtcNow.AddMinutes(-5));

            loaded.Rename($"Renamed {Guid.NewGuid():N}");
            context.SaveChanges();
        }

        using var check = fixture.CreateContext();
        var reloaded = check.Chairs.Single(c => c.Id == chair.Id);
        var createdAt = check.Entry(reloaded).Property<DateTimeOffset>("CreatedAt").CurrentValue;
        var updatedAt = check.Entry(reloaded).Property<DateTimeOffset>("UpdatedAt").CurrentValue;

        Assert.Equal(createdAtFirst, createdAt);
        Assert.True(updatedAt >= createdAt);
    }
}