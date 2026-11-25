using Domain.Common;
using Domain.Scheduling;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using static Infrastructure.Tests.TestData;

namespace Infrastructure.Tests;

/// <summary>All data is fictitious.</summary>
public class ScheduleBlockRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static ScheduleBlock NewBlock(Guid? dentistId, string reason = "Vacaciones") =>
        ScheduleBlock.Create(dentistId, new TimeRange(MondayAt(8, 0), MondayAt(12, 0)), reason);

    [Fact]
    public async Task Add_SavesTheBlockOfADentistInUtc()
    {
        var (user, dentist) = NewDentist();
        fixture.Save(user, dentist);
        var block = NewBlock(dentist.Id);

        await using (var context = fixture.CreateContext())
        {
            new ScheduleBlockRepository(context).Add(block);
            await context.SaveChangesAsync();
        }

        await using var reading = fixture.CreateContext();
        var loaded = await new ScheduleBlockRepository(reading).GetByIdAsync(block.Id);

        Assert.NotNull(loaded);
        Assert.Equal(dentist.Id, loaded.DentistId);
        Assert.Equal(new DateTimeOffset(2026, 12, 7, 13, 0, 0, TimeSpan.Zero), loaded.Range.Start);
        Assert.Equal(TimeSpan.Zero, loaded.Range.Start.Offset);
        Assert.Equal(new DateTimeOffset(2026, 12, 7, 17, 0, 0, TimeSpan.Zero), loaded.Range.End);
        Assert.Equal("Vacaciones", loaded.Reason);
    }

    [Fact]
    public async Task GetById_ReturnsATrackedBlock()
    {
        var block = NewBlock(null, "Feriado");
        fixture.Save(block);

        await using var context = fixture.CreateContext();
        var loaded = await new ScheduleBlockRepository(context).GetByIdAsync(block.Id);

        Assert.NotNull(loaded);
        Assert.Null(loaded.DentistId);
        Assert.Equal(EntityState.Unchanged, context.Entry(loaded).State);
    }

    [Fact]
    public async Task GetById_WithAnUnknownId_ReturnsNull()
    {
        await using var context = fixture.CreateContext();

        Assert.Null(await new ScheduleBlockRepository(context).GetByIdAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task Remove_DeletesOnlyThatBlock()
    {
        var removed = NewBlock(null, "Feriado");
        var kept = NewBlock(null, "Feriado");
        fixture.Save(removed, kept);

        await using (var context = fixture.CreateContext())
        {
            var repository = new ScheduleBlockRepository(context);
            repository.Remove((await repository.GetByIdAsync(removed.Id))!);
            await context.SaveChangesAsync();
        }

        await using var reading = fixture.CreateContext();
        var repositoryAfter = new ScheduleBlockRepository(reading);
        Assert.Null(await repositoryAfter.GetByIdAsync(removed.Id));
        Assert.NotNull(await repositoryAfter.GetByIdAsync(kept.Id));
    }

    // List. The database is shared by the tests of this class, so they check their own blocks by id.

    // March 2027, far from the blocks of the other tests.
    private static DateTimeOffset MarchAt(int day, int hour) => new(2027, 3, day, hour, 0, 0, TimeSpan.FromHours(-5));

    private static readonly TimeRange March10 = new(MarchAt(10, 0), MarchAt(11, 0));

    private ScheduleBlock SaveBlock(Guid? dentistId, DateTimeOffset start, DateTimeOffset end)
    {
        var block = ScheduleBlock.Create(dentistId, new TimeRange(start, end), "Bloqueo");
        fixture.Save(block);
        return block;
    }

    private Guid SaveDentist()
    {
        var (user, dentist) = NewDentist();
        fixture.Save(user, dentist);
        return dentist.Id;
    }

    private async Task<List<Guid>> ListIdsAsync(TimeRange range, Guid? dentistId, bool includeClinicWide)
    {
        await using var context = fixture.CreateContext();
        var list = await new ScheduleBlockRepository(context).ListAsync(range, dentistId, includeClinicWide);
        return list.Select(b => b.Id).ToList();
    }

    [Fact]
    public async Task List_ReturnsTheBlocksThatOverlapTheRange_AndNotTheOnesThatOnlyTouchIt()
    {
        var inside = SaveBlock(null, MarchAt(10, 8), MarchAt(10, 12));
        var crossingTheStart = SaveBlock(null, MarchAt(9, 20), MarchAt(10, 1));
        var crossingTheEnd = SaveBlock(null, MarchAt(10, 23), MarchAt(11, 2));
        var covering = SaveBlock(null, MarchAt(1, 0), MarchAt(31, 0));
        var endingAtTheStart = SaveBlock(null, MarchAt(9, 20), MarchAt(10, 0));
        var startingAtTheEnd = SaveBlock(null, MarchAt(11, 0), MarchAt(11, 4));

        var ids = await ListIdsAsync(March10, dentistId: null, includeClinicWide: true);

        Assert.Contains(inside.Id, ids);
        Assert.Contains(crossingTheStart.Id, ids);
        Assert.Contains(crossingTheEnd.Id, ids);
        Assert.Contains(covering.Id, ids);
        Assert.DoesNotContain(endingAtTheStart.Id, ids);
        Assert.DoesNotContain(startingAtTheEnd.Id, ids);
    }

    [Fact]
    public async Task List_ForADentist_ReturnsTheirBlocksAndTheClinicOnes_OnlyWhenAsked()
    {
        var dentistId = SaveDentist();
        var own = SaveBlock(dentistId, MarchAt(10, 8), MarchAt(10, 12));
        var otherDentist = SaveBlock(SaveDentist(), MarchAt(10, 8), MarchAt(10, 12));
        var clinic = SaveBlock(null, MarchAt(10, 8), MarchAt(10, 18));

        var withClinic = await ListIdsAsync(March10, dentistId, includeClinicWide: true);
        var withoutClinic = await ListIdsAsync(March10, dentistId, includeClinicWide: false);

        Assert.Contains(own.Id, withClinic);
        Assert.Contains(clinic.Id, withClinic);
        Assert.DoesNotContain(otherDentist.Id, withClinic);
        Assert.Equal([own.Id], withoutClinic);
    }

    [Fact]
    public async Task List_WithoutADentist_ReturnsEveryDentist_AndTheClinicOnesOnlyWhenAsked()
    {
        var first = SaveBlock(SaveDentist(), MarchAt(10, 8), MarchAt(10, 12));
        var second = SaveBlock(SaveDentist(), MarchAt(10, 14), MarchAt(10, 16));
        var clinic = SaveBlock(null, MarchAt(10, 8), MarchAt(10, 18));

        var withClinic = await ListIdsAsync(March10, dentistId: null, includeClinicWide: true);
        var withoutClinic = await ListIdsAsync(March10, dentistId: null, includeClinicWide: false);

        Assert.Contains(first.Id, withClinic);
        Assert.Contains(second.Id, withClinic);
        Assert.Contains(clinic.Id, withClinic);
        Assert.Contains(first.Id, withoutClinic);
        Assert.Contains(second.Id, withoutClinic);
        Assert.DoesNotContain(clinic.Id, withoutClinic);
    }

    [Fact]
    public async Task List_IsOrderedByStart()
    {
        var dentistId = SaveDentist();
        var later = SaveBlock(dentistId, MarchAt(10, 14), MarchAt(10, 16));
        var earlier = SaveBlock(dentistId, MarchAt(10, 8), MarchAt(10, 9));

        var ids = await ListIdsAsync(March10, dentistId, includeClinicWide: false);

        Assert.Equal([earlier.Id, later.Id], ids);
    }

    [Fact]
    public async Task List_ReturnsUntrackedBlocks()
    {
        SaveBlock(null, MarchAt(10, 8), MarchAt(10, 9));

        await using var context = fixture.CreateContext();
        var list = await new ScheduleBlockRepository(context).ListAsync(March10, dentistId: null, includeClinicWide: true);

        Assert.NotEmpty(list);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public void Save_WithADentistThatDoesNotExist_IsRejectedByTheForeignKey()
    {
        var ex = Assert.Throws<DbUpdateException>(() => fixture.Save(NewBlock(Guid.CreateVersion7())));

        var postgres = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgres.SqlState);
    }
}
