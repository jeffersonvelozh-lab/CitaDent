using Domain.Appointments;
using Domain.Scheduling;

namespace Infrastructure.Tests;

internal static class TestData
{
    // Ecuador has no daylight saving time, so a fixed UTC-5 zone behaves the same on any machine.
    public static readonly TimeZoneInfo Ecuador =
        TimeZoneInfo.CreateCustomTimeZone("ecuador-test", TimeSpan.FromHours(-5), "Ecuador", "Ecuador");

    // December 7, 2026 is a Monday.
    public static DateTimeOffset MondayAt(int hour, int minute) =>
        new(2026, 12, 7, hour, minute, 0, TimeSpan.FromHours(-5));

    /// <summary>A dentist who works Monday 08:00-12:00 and 14:00-18:00.</summary>
    public static Dentist NewDentist()
    {
        var dentist = Dentist.Create(Guid.NewGuid(), "Carla", "Mora", "LIC-123");

        dentist.SetWeeklySchedule(
        [
            new WorkingHours(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            new WorkingHours(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(18, 0))
        ]);

        return dentist;
    }

    // Chair names are unique in the database, so each test gets its own.
    public static Chair NewChair() => Chair.Create($"Chair {Guid.NewGuid():N}");

    /// <summary>
    /// Builds an appointment with an empty scheduling context on purpose: the domain cannot see the other
    /// appointments, so a conflict can only be stopped by the database.
    /// </summary>
    public static Appointment ScheduleBlindly(PostgresFixture fixture, Dentist dentist, Chair chair, DateTimeOffset start) =>
        Appointment.Schedule(
            fixture.SeedPatient,
            dentist,
            chair,
            fixture.SeedProcedure,
            start,
            Guid.NewGuid(),
            new SchedulingContext(Ecuador, [], []));
}