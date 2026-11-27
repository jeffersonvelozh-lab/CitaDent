using Application.Appointments;
using Application.Common;
using Domain.Appointments;
using Domain.Common;
using Domain.Scheduling;
using Infrastructure.Persistence.Repositories;
using Xunit;
using static Infrastructure.Tests.TestData;

namespace Infrastructure.Tests;

/// <summary>
/// Loading the scheduling context: only the Scheduled appointments and the blocks that overlap the requested slot,
/// for its dentist and chair; and the agenda of a range. Each test uses its own dentists and chairs. All data is
/// fictitious.
/// </summary>
public class AppointmentRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // Monday, December 7, 2026, 09:00 to 09:30 at the clinic: the slot being requested.
    private static readonly TimeRange Slot = new(MondayAt(9, 0), MondayAt(9, 30));

    private Dentist SaveDentist()
    {
        var (user, dentist) = NewDentist();
        fixture.Save(user, dentist);
        return dentist;
    }

    private Chair SaveChair()
    {
        var chair = NewChair();
        fixture.Save(chair);
        return chair;
    }

    private Appointment Save(Appointment appointment)
    {
        fixture.Save(appointment);
        return appointment;
    }

    private async Task<List<Guid>> ListIdsAsync(Dentist dentist, Chair chair, TimeRange? slot = null)
    {
        await using var context = fixture.CreateContext();
        var list = await new AppointmentRepository(context)
            .ListScheduledOverlappingAsync(slot ?? Slot, dentist.Id, chair.Id);
        return list.Select(a => a.Id).ToList();
    }

    [Fact]
    public async Task List_ReturnsTheOverlappingAppointmentsOfTheDentistOrTheChair_AndNoOthers()
    {
        var (dentist, chair) = (SaveDentist(), SaveChair());
        var (otherDentist, otherChair) = (SaveDentist(), SaveChair());
        var (thirdDentist, thirdChair) = (SaveDentist(), SaveChair());

        var ofTheDentist = Save(ScheduleBlindly(fixture, dentist, otherChair, MondayAt(9, 15)));
        var ofTheChair = Save(ScheduleBlindly(fixture, otherDentist, chair, MondayAt(8, 45)));
        var unrelated = Save(ScheduleBlindly(fixture, thirdDentist, thirdChair, MondayAt(9, 0)));

        var ids = await ListIdsAsync(dentist, chair);

        Assert.Equal([ofTheChair.Id, ofTheDentist.Id], ids);
        Assert.DoesNotContain(unrelated.Id, ids);
    }

    [Fact]
    public async Task List_LeavesOutAppointmentsThatNoLongerOccupyTime()
    {
        var (dentist, chair) = (SaveDentist(), SaveChair());

        var cancelled = ScheduleBlindly(fixture, dentist, chair, MondayAt(9, 0));
        cancelled.Cancel("El paciente no puede asistir");
        var completed = ScheduleBlindly(fixture, dentist, chair, MondayAt(9, 0));
        completed.Complete();
        var noShow = ScheduleBlindly(fixture, dentist, chair, MondayAt(9, 0));
        noShow.MarkNoShow();
        fixture.Save(cancelled, completed, noShow);

        Assert.Empty(await ListIdsAsync(dentist, chair));
    }

    [Fact]
    public async Task List_LeavesOutAppointmentsThatOnlyTouchTheSlotOrAreOnAnotherDay()
    {
        var (dentist, chair) = (SaveDentist(), SaveChair());

        Save(ScheduleBlindly(fixture, dentist, chair, MondayAt(8, 30))); // Ends at 09:00 (RN-04).
        Save(ScheduleBlindly(fixture, dentist, chair, MondayAt(9, 30))); // Starts at 09:30.
        Save(ScheduleBlindly(fixture, dentist, chair, MondayAt(9, 0).AddDays(7))); // The next Monday.

        Assert.Empty(await ListIdsAsync(dentist, chair));
    }

    [Fact]
    public async Task List_ReturnsUntrackedAppointments()
    {
        var (dentist, chair) = (SaveDentist(), SaveChair());
        Save(ScheduleBlindly(fixture, dentist, chair, MondayAt(9, 0)));

        await using var context = fixture.CreateContext();
        var list = await new AppointmentRepository(context).ListScheduledOverlappingAsync(Slot, dentist.Id, chair.Id);

        Assert.Single(list);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    // The agenda: every state, with the names joined, filtered by range and optionally by dentist.

    private async Task<IReadOnlyList<AgendaItemDto>> ListAgendaAsync(TimeRange range, Guid? dentistId)
    {
        await using var context = fixture.CreateContext();
        return await new AppointmentRepository(context).ListAgendaAsync(range, dentistId);
    }

    // All of Monday, December 7, at the clinic.
    private static readonly TimeRange Monday = new(MondayAt(0, 0), MondayAt(0, 0).AddDays(1));

    [Fact]
    public async Task ListAgenda_ReturnsTheAppointmentWithTheNamesOfEveryRecord()
    {
        var (dentist, chair) = (SaveDentist(), SaveChair());
        var appointment = Save(ScheduleBlindly(fixture, dentist, chair, MondayAt(9, 0)));

        var item = Assert.Single(await ListAgendaAsync(Monday, dentist.Id));

        Assert.Equal(appointment.Id, item.Id);
        Assert.Equal(MondayAt(9, 0), item.StartsAt);
        Assert.Equal(MondayAt(9, 30), item.EndsAt);
        Assert.Equal(AppointmentStatus.Scheduled, item.Status);
        Assert.Null(item.CancelReason);
        Assert.Equal(fixture.SeedPatient.Id, item.PatientId);
        Assert.Equal("Ana Pérez", item.PatientName);
        Assert.Equal(dentist.Id, item.DentistId);
        Assert.Equal("Carla Mora", item.DentistName);
        Assert.Equal(chair.Id, item.ChairId);
        Assert.Equal(chair.Name, item.ChairName);
        Assert.Equal(fixture.SeedProcedure.Id, item.ProcedureId);
        Assert.Equal("Cleaning", item.ProcedureName);
    }

    [Fact]
    public async Task ListAgenda_IncludesEveryState_OrderedByStart()
    {
        var dentist = SaveDentist();

        var cancelled = ScheduleBlindly(fixture, dentist, SaveChair(), MondayAt(11, 0));
        cancelled.Cancel("El paciente no puede asistir");
        var completed = ScheduleBlindly(fixture, dentist, SaveChair(), MondayAt(9, 0));
        completed.Complete();
        var noShow = ScheduleBlindly(fixture, dentist, SaveChair(), MondayAt(10, 0));
        noShow.MarkNoShow();
        var scheduled = ScheduleBlindly(fixture, dentist, SaveChair(), MondayAt(14, 0));
        fixture.Save(cancelled, completed, noShow, scheduled);

        var list = await ListAgendaAsync(Monday, dentist.Id);

        Assert.Equal([completed.Id, noShow.Id, cancelled.Id, scheduled.Id], list.Select(a => a.Id));
        Assert.Equal("El paciente no puede asistir", list[2].CancelReason);
    }

    [Fact]
    public async Task ListAgenda_LeavesOutAppointmentsThatOnlyTouchTheRange()
    {
        var (dentist, chair) = (SaveDentist(), SaveChair());
        var inside = Save(ScheduleBlindly(fixture, dentist, chair, MondayAt(9, 0)));
        Save(ScheduleBlindly(fixture, dentist, chair, MondayAt(8, 30))); // Ends at 09:00, when the range starts.
        Save(ScheduleBlindly(fixture, dentist, chair, MondayAt(9, 30))); // Starts at 09:30, when the range ends.

        var list = await ListAgendaAsync(Slot, dentist.Id);

        Assert.Equal(inside.Id, Assert.Single(list).Id);
    }

    [Fact]
    public async Task ListAgenda_ReadsTheRangeAsInstants_SoALateClinicAppointmentStaysOnItsDay()
    {
        // 23:00 on Monday at the clinic is 04:00 UTC on Tuesday, but it belongs to Monday.
        var (user, dentist) = NewDentist();
        dentist.SetWeeklySchedule([new WorkingHours(DayOfWeek.Monday, new TimeOnly(20, 0), new TimeOnly(23, 59))]);
        fixture.Save(user, dentist);
        var lateAtNight = Save(ScheduleBlindly(fixture, dentist, SaveChair(), MondayAt(23, 0)));

        var monday = await ListAgendaAsync(Monday, dentist.Id);
        var tuesday = await ListAgendaAsync(new TimeRange(Monday.End, Monday.End.AddDays(1)), dentist.Id);

        Assert.Equal(lateAtNight.Id, Assert.Single(monday).Id);
        Assert.Empty(tuesday);
    }

    [Fact]
    public async Task ListAgenda_WithoutADentist_ReturnsEveryDentist()
    {
        var (dentist, otherDentist) = (SaveDentist(), SaveDentist());
        var first = Save(ScheduleBlindly(fixture, dentist, SaveChair(), MondayAt(9, 0).AddDays(14)));
        var second = Save(ScheduleBlindly(fixture, otherDentist, SaveChair(), MondayAt(9, 0).AddDays(14)));

        var ids = (await ListAgendaAsync(new TimeRange(Monday.Start.AddDays(14), Monday.End.AddDays(14)), null))
            .Select(a => a.Id)
            .ToList();

        Assert.Contains(first.Id, ids);
        Assert.Contains(second.Id, ids);
    }

    [Fact]
    public async Task ListAgenda_ForADentist_LeavesOutTheOthers()
    {
        var (dentist, otherDentist) = (SaveDentist(), SaveDentist());
        var own = Save(ScheduleBlindly(fixture, dentist, SaveChair(), MondayAt(9, 0)));
        Save(ScheduleBlindly(fixture, otherDentist, SaveChair(), MondayAt(9, 0)));

        var list = await ListAgendaAsync(Monday, dentist.Id);

        Assert.Equal(own.Id, Assert.Single(list).Id);
    }

    [Fact]
    public async Task ListAgenda_ForAnUnknownDentist_IsEmpty()
    {
        Assert.Empty(await ListAgendaAsync(Monday, Guid.CreateVersion7()));
    }

    // The loader with the real repositories: what it loads is enough for the domain to apply the rules.

    private async Task<SchedulingContext> LoadAsync(Dentist dentist, Chair chair)
    {
        await using var context = fixture.CreateContext();
        var loader = new SchedulingContextLoader(
            new ScheduleBlockRepository(context),
            new AppointmentRepository(context),
            new ClinicClock(TimeProvider.System, Ecuador));

        return await loader.LoadAsync(dentist.Id, chair.Id, Slot);
    }

    private Appointment ScheduleInTheSlot(Dentist dentist, Chair chair, SchedulingContext context) =>
        Appointment.Schedule(
            fixture.SeedPatient, dentist, chair, fixture.SeedProcedure, Slot.Start, fixture.SeedUser.Id, context);

    [Fact]
    public async Task Load_BringsOnlyTheRelevantBlocksAndAppointments()
    {
        var (dentist, chair) = (SaveDentist(), SaveChair());
        var (otherDentist, otherChair) = (SaveDentist(), SaveChair());

        var ownBlock = ScheduleBlock.Create(dentist.Id, new TimeRange(MondayAt(8, 0), MondayAt(10, 0)), "Vacaciones");
        var otherDentistBlock = ScheduleBlock.Create(otherDentist.Id, new TimeRange(MondayAt(8, 0), MondayAt(10, 0)), "Vacaciones");
        var laterBlock = ScheduleBlock.Create(dentist.Id, new TimeRange(MondayAt(14, 0), MondayAt(16, 0)), "Curso");
        fixture.Save(ownBlock, otherDentistBlock, laterBlock);

        var ofTheChair = Save(ScheduleBlindly(fixture, otherDentist, chair, MondayAt(9, 0)));
        var later = Save(ScheduleBlindly(fixture, dentist, chair, MondayAt(11, 0)));
        var elsewhere = Save(ScheduleBlindly(fixture, otherDentist, otherChair, MondayAt(10, 0)));

        var context = await LoadAsync(dentist, chair);

        var blockIds = context.Blocks.Select(b => b.Id).ToList();
        Assert.Contains(ownBlock.Id, blockIds);
        Assert.DoesNotContain(otherDentistBlock.Id, blockIds);
        Assert.DoesNotContain(laterBlock.Id, blockIds);

        Assert.Equal([ofTheChair.Id], context.ExistingAppointments.Select(a => a.Id));
        Assert.DoesNotContain(later.Id, context.ExistingAppointments.Select(a => a.Id));
        Assert.DoesNotContain(elsewhere.Id, context.ExistingAppointments.Select(a => a.Id));
        Assert.Equal(Ecuador, context.ClinicTimeZone);
    }

    [Fact]
    public async Task Load_GivesTheDomainWhatItNeedsToRejectABlockedSlot_RN05()
    {
        var (dentist, chair) = (SaveDentist(), SaveChair());
        fixture.Save(ScheduleBlock.Create(dentist.Id, new TimeRange(MondayAt(8, 0), MondayAt(10, 0)), "Vacaciones"));

        var context = await LoadAsync(dentist, chair);

        var ex = Assert.Throws<DomainException>(() => ScheduleInTheSlot(dentist, chair, context));
        Assert.Equal("RN-05", ex.Code);
    }

    [Fact]
    public async Task Load_GivesTheDomainWhatItNeedsToRejectABusyDentist_RN02()
    {
        var (dentist, chair) = (SaveDentist(), SaveChair());
        Save(ScheduleBlindly(fixture, dentist, SaveChair(), MondayAt(9, 0)));

        var context = await LoadAsync(dentist, chair);

        var ex = Assert.Throws<DomainException>(() => ScheduleInTheSlot(dentist, chair, context));
        Assert.Equal("RN-02", ex.Code);
    }

    [Fact]
    public async Task Load_GivesTheDomainWhatItNeedsToRejectABusyChair_RN03()
    {
        var (dentist, chair) = (SaveDentist(), SaveChair());
        Save(ScheduleBlindly(fixture, SaveDentist(), chair, MondayAt(9, 0)));

        var context = await LoadAsync(dentist, chair);

        var ex = Assert.Throws<DomainException>(() => ScheduleInTheSlot(dentist, chair, context));
        Assert.Equal("RN-03", ex.Code);
    }

    [Fact]
    public async Task Load_WithAFreeSlot_LetsTheDomainScheduleIt()
    {
        var (dentist, chair) = (SaveDentist(), SaveChair());
        Save(ScheduleBlindly(fixture, dentist, chair, MondayAt(8, 30))); // Back to back (RN-04).

        var context = await LoadAsync(dentist, chair);

        Assert.Equal(Slot, ScheduleInTheSlot(dentist, chair, context).Slot);
    }
}
