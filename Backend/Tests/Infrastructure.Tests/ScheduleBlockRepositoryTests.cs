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

    [Fact]
    public void Save_WithADentistThatDoesNotExist_IsRejectedByTheForeignKey()
    {
        var ex = Assert.Throws<DbUpdateException>(() => fixture.Save(NewBlock(Guid.CreateVersion7())));

        var postgres = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgres.SqlState);
    }
}
