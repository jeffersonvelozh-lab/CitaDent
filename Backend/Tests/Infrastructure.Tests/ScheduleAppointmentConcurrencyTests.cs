using Application.Appointments;
using Application.Common;
using Domain.Appointments;
using Domain.Common;
using Domain.Scheduling;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using static Infrastructure.Tests.TestData;

namespace Infrastructure.Tests;

/// <summary>
/// Booking against the real PostgreSQL: the 23P01 of the exclusion constraints becomes ScheduleConflictException,
/// and two requests for the same time at once end with one appointment and RN-02 or RN-03 for the other.
/// Each test uses its own dentists and chairs. All data is fictitious.
/// </summary>
public class ScheduleAppointmentConcurrencyTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // Tuesday, December 1, 2026, 07:00 at the clinic, so Monday, December 7 is in the future.
    private static readonly DateTimeOffset Now = new(2026, 12, 1, 12, 0, 0, TimeSpan.Zero);

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

    // The DbContext turns 23P01 into ScheduleConflictException

    [Fact]
    public async Task SaveChanges_WithAnOverlapOfTheDentist_ThrowsADentistConflict()
    {
        var dentist = SaveDentist();
        fixture.Save(ScheduleBlindly(fixture, dentist, SaveChair(), MondayAt(9, 0)));

        await using var context = fixture.CreateContext();
        context.Appointments.Add(ScheduleBlindly(fixture, dentist, SaveChair(), MondayAt(9, 15)));

        var ex = await Assert.ThrowsAsync<ScheduleConflictException>(() => context.SaveChangesAsync());

        Assert.Equal(ScheduleConflict.Dentist, ex.Conflict);
        var postgres = Assert.IsType<PostgresException>(Assert.IsType<DbUpdateException>(ex.InnerException).InnerException);
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, postgres.SqlState);
    }

    [Fact]
    public async Task SaveChanges_WithAnOverlapOfTheChair_ThrowsAChairConflict()
    {
        var chair = SaveChair();
        fixture.Save(ScheduleBlindly(fixture, SaveDentist(), chair, MondayAt(9, 0)));

        await using var context = fixture.CreateContext();
        context.Appointments.Add(ScheduleBlindly(fixture, SaveDentist(), chair, MondayAt(9, 15)));

        var ex = await Assert.ThrowsAsync<ScheduleConflictException>(() => context.SaveChangesAsync());

        Assert.Equal(ScheduleConflict.Chair, ex.Conflict);
    }

    [Fact]
    public async Task SaveChanges_WithAnotherDatabaseError_LeavesItAsIs()
    {
        await using var context = fixture.CreateContext();
        context.ScheduleBlocks.Add(ScheduleBlock.Create(
            Guid.CreateVersion7(), new TimeRange(MondayAt(8, 0), MondayAt(9, 0)), "Dentist that does not exist"));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState);
    }

    [Fact]
    public void TheDatabaseOptions_RetryTransientErrors_SoADeadlockBetweenTwoBookingsIsRetried()
    {
        // Two overlapping inserts at once can deadlock (40P01) instead of failing with 23P01; the retry turns
        // the loser into a clean 23P01. The concurrency tests below hit that path only some of the time.
        using var context = fixture.CreateContext();

        Assert.True(context.Database.CreateExecutionStrategy().RetriesOnFailure);
    }

    // Two requests at the same time (CA of 1b-15)

    private ScheduleAppointmentHandler NewHandler(CitaDentDbContext context, IUnitOfWork unitOfWork)
    {
        var clock = new ClinicClock(new StoppedClock(Now), Ecuador);
        var appointments = new AppointmentRepository(context);

        return new ScheduleAppointmentHandler(
            new PatientRepository(context),
            new DentistRepository(context),
            new ChairRepository(context),
            new ProcedureRepository(context),
            appointments,
            new SchedulingContextLoader(new ScheduleBlockRepository(context), appointments, clock),
            clock,
            unitOfWork);
    }

    private ScheduleAppointmentCommand Command(Dentist dentist, Chair chair) =>
        new(fixture.SeedPatient.Id, dentist.Id, chair.Id, fixture.SeedProcedure.Id, MondayAt(9, 0), fixture.SeedUser.Id);

    /// <summary>
    /// Runs both commands at once, each with its own DbContext like two separate requests. Neither saves until both
    /// have loaded their context and passed the domain checks, so both believe the slot is free and only the
    /// database can stop one. Returns the error code of each attempt, or null for the one that was saved.
    /// </summary>
    private async Task<string?[]> ScheduleAtTheSameTimeAsync(ScheduleAppointmentCommand first, ScheduleAppointmentCommand second)
    {
        using var bothReadyToSave = new Barrier(participantCount: 2);

        var attempts = new[] { first, second }.Select(command => Task.Run(async () =>
        {
            await using var context = fixture.CreateContext();
            var handler = NewHandler(context, new SaveWhenBothAreReady(context, bothReadyToSave));

            try
            {
                await handler.HandleAsync(command);
                return (string?)null;
            }
            catch (DomainException ex)
            {
                return ex.Code;
            }
        }));

        return await Task.WhenAll(attempts);
    }

    private int CountScheduled(Func<Appointment, bool> belongs)
    {
        using var context = fixture.CreateContext();
        return context.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Scheduled)
            .AsEnumerable()
            .Count(belongs);
    }

    [Fact]
    public async Task TwoRequestsForTheSameDentistAndTime_OneWinsAndTheOtherGetsRN02()
    {
        var dentist = SaveDentist();

        var results = await ScheduleAtTheSameTimeAsync(Command(dentist, SaveChair()), Command(dentist, SaveChair()));

        Assert.Single(results, code => code is null);
        Assert.Single(results, code => code == "RN-02");
        Assert.Equal(1, CountScheduled(a => a.DentistId == dentist.Id));
    }

    [Fact]
    public async Task TwoRequestsForTheSameChairAndTime_OneWinsAndTheOtherGetsRN03()
    {
        var chair = SaveChair();

        var results = await ScheduleAtTheSameTimeAsync(Command(SaveDentist(), chair), Command(SaveDentist(), chair));

        Assert.Single(results, code => code is null);
        Assert.Single(results, code => code == "RN-03");
        Assert.Equal(1, CountScheduled(a => a.ChairId == chair.Id));
    }

    [Fact]
    public async Task TwoRequestsForDifferentDentistsAndChairs_BothAreSaved()
    {
        var (dentist1, chair1) = (SaveDentist(), SaveChair());
        var (dentist2, chair2) = (SaveDentist(), SaveChair());

        var results = await ScheduleAtTheSameTimeAsync(Command(dentist1, chair1), Command(dentist2, chair2));

        Assert.All(results, code => Assert.Null(code));
        Assert.Equal(2, CountScheduled(a => a.DentistId == dentist1.Id || a.DentistId == dentist2.Id));
    }

    /// <summary>Waits until both requests reach the save, then saves. Fails instead of hanging if one never arrives.</summary>
    private sealed class SaveWhenBothAreReady(IUnitOfWork inner, Barrier barrier) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(30), cancellationToken))
            {
                throw new TimeoutException("The other request never reached the save.");
            }

            return inner.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class StoppedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
