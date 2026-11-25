using Application.Appointments;
using Application.Tests.Fakes;
using Domain.Common;
using Domain.Scheduling;

namespace Application.Tests.Appointments;

/// <summary>How the scheduling context is put together, with in-memory doubles.</summary>
public class SchedulingContextLoaderTests
{
    private static readonly TimeSpan Ecuador = TimeSpan.FromHours(-5);

    private static readonly Guid DentistId = Guid.CreateVersion7();
    private static readonly Guid ChairId = Guid.CreateVersion7();

    // December 7, 2026, 09:00 to 09:30 at the clinic.
    private static readonly TimeRange Slot = new(
        new DateTimeOffset(2026, 12, 7, 9, 0, 0, Ecuador),
        new DateTimeOffset(2026, 12, 7, 9, 30, 0, Ecuador));

    private readonly FakeScheduleBlockRepository _blocks = new();
    private readonly FakeAppointmentRepository _appointments = new();

    private SchedulingContextLoader Loader =>
        new(_blocks, _appointments, TestClinic.ClockAt(new DateTimeOffset(2026, 12, 1, 12, 0, 0, TimeSpan.Zero)));

    private ScheduleBlock SeedBlock(Guid? dentistId, int fromHour, int toHour) =>
        _blocks.Seed(ScheduleBlock.Create(
            dentistId,
            new TimeRange(new DateTimeOffset(2026, 12, 7, fromHour, 0, 0, Ecuador), new DateTimeOffset(2026, 12, 7, toHour, 0, 0, Ecuador)),
            "Bloqueo"));

    [Fact]
    public async Task Load_UsesTheClinicTimeZone()
    {
        var context = await Loader.LoadAsync(DentistId, ChairId, Slot);

        Assert.Equal(TestClinic.Ecuador, context.ClinicTimeZone);
    }

    [Fact]
    public async Task Load_TakesTheBlocksOfTheDentistAndOfTheClinicThatOverlapTheSlot()
    {
        var own = SeedBlock(DentistId, 8, 10);
        var clinic = SeedBlock(null, 9, 12);
        SeedBlock(Guid.CreateVersion7(), 8, 10); // Another dentist.
        SeedBlock(DentistId, 10, 12); // Later that day.
        SeedBlock(null, 8, 9); // Ends when the slot starts.

        var context = await Loader.LoadAsync(DentistId, ChairId, Slot);

        Assert.Equal([own.Id, clinic.Id], context.Blocks.Select(b => b.Id));
    }

    [Fact]
    public async Task Load_AsksForTheAppointmentsOfTheSlotDentistAndChair_AndKeepsThem()
    {
        var existing = ScheduleAnAppointment();
        _appointments.Overlapping.Add(existing);

        var context = await Loader.LoadAsync(DentistId, ChairId, Slot);

        Assert.Equal((Slot, DentistId, ChairId), _appointments.LastQuery);
        Assert.Equal(existing.Id, Assert.Single(context.ExistingAppointments).Id);
    }

    // A real appointment, built with an empty context. Fictitious data.
    private static Domain.Appointments.Appointment ScheduleAnAppointment()
    {
        var dentist = Dentist.Create(Guid.CreateVersion7(), "Carla", "Mora", "LIC-001");
        dentist.SetWeeklySchedule([new WorkingHours(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0))]);

        return Domain.Appointments.Appointment.Schedule(
            Domain.Patients.Patient.Create(
                Domain.Patients.DocumentType.Cedula, "1710034065", "Ana", "Pérez", new DateOnly(1990, 5, 20), new DateOnly(2026, 12, 1)),
            dentist,
            Chair.Create("Sillón 1"),
            Domain.Procedures.Procedure.Create("Limpieza", null, 30, 25m),
            Slot.Start,
            Guid.CreateVersion7(),
            new Domain.Appointments.SchedulingContext(TestClinic.Ecuador, [], []));
    }

    [Fact]
    public async Task Load_WithoutASlot_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Loader.LoadAsync(DentistId, ChairId, null!));
    }
}
