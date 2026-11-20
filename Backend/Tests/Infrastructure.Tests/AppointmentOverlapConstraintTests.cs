using Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using static Infrastructure.Tests.TestData;

namespace Infrastructure.Tests;

/// <summary>
/// Proves that PostgreSQL itself rejects overlapping appointments (RN-02, RN-03), even when the domain
/// is not given the other appointments and so cannot notice the conflict.
/// </summary>
public class AppointmentOverlapConstraintTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public void TheDatabaseRejects_TwoScheduledAppointments_OfTheSameDentistAtOverlappingTimes()
    {
        var dentist = NewDentist();
        var chair1 = NewChair();
        var chair2 = NewChair();
        fixture.Save(dentist, chair1, chair2, ScheduleBlindly(fixture, dentist, chair1, MondayAt(9, 0)));

        var overlapping = ScheduleBlindly(fixture, dentist, chair2, MondayAt(9, 15));

        var ex = Assert.Throws<DbUpdateException>(() => fixture.Save(overlapping));
        var postgres = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, postgres.SqlState);
        Assert.Equal("ex_appointments_dentist_no_overlap", postgres.ConstraintName);
    }

    [Fact]
    public void TheDatabaseRejects_TwoScheduledAppointments_InTheSameChairAtOverlappingTimes()
    {
        var dentist1 = NewDentist();
        var dentist2 = NewDentist();
        var chair = NewChair();
        fixture.Save(dentist1, dentist2, chair, ScheduleBlindly(fixture, dentist1, chair, MondayAt(9, 0)));

        var overlapping = ScheduleBlindly(fixture, dentist2, chair, MondayAt(9, 15));

        var ex = Assert.Throws<DbUpdateException>(() => fixture.Save(overlapping));
        var postgres = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, postgres.SqlState);
        Assert.Equal("ex_appointments_chair_no_overlap", postgres.ConstraintName);
    }

    [Fact]
    public void TheDatabaseAllows_BackToBackAppointments()
    {
        var dentist = NewDentist();
        var chair = NewChair();
        fixture.Save(dentist, chair, ScheduleBlindly(fixture, dentist, chair, MondayAt(9, 0)));

        fixture.Save(ScheduleBlindly(fixture, dentist, chair, MondayAt(9, 30)));

        using var context = fixture.CreateContext();
        Assert.Equal(2, context.Appointments.Count(a => a.DentistId == dentist.Id));
    }

    [Fact]
    public void TheDatabaseAllows_TheSameTime_ForADifferentDentistAndChair()
    {
        var dentist1 = NewDentist();
        var dentist2 = NewDentist();
        var chair1 = NewChair();
        var chair2 = NewChair();
        fixture.Save(dentist1, dentist2, chair1, chair2, ScheduleBlindly(fixture, dentist1, chair1, MondayAt(9, 0)));

        fixture.Save(ScheduleBlindly(fixture, dentist2, chair2, MondayAt(9, 0)));

        using var context = fixture.CreateContext();
        Assert.Equal(1, context.Appointments.Count(a => a.DentistId == dentist2.Id));
    }

    [Fact]
    public void ACancelledAppointment_NoLongerBlocksItsTime()
    {
        var dentist = NewDentist();
        var chair = NewChair();
        var first = ScheduleBlindly(fixture, dentist, chair, MondayAt(9, 0));
        fixture.Save(dentist, chair, first);

        using (var context = fixture.CreateContext())
        {
            var stored = context.Appointments.Single(a => a.Id == first.Id);
            stored.Cancel("Patient called to cancel");
            context.SaveChanges();
        }

        fixture.Save(ScheduleBlindly(fixture, dentist, chair, MondayAt(9, 0)));

        using var check = fixture.CreateContext();
        Assert.Equal(2, check.Appointments.Count(a => a.DentistId == dentist.Id));
    }
}