using Domain.Appointments;
using Domain.Procedures;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Infrastructure.Tests.TestData;

namespace Infrastructure.Tests;

public class ProcedureRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // Names are unique in the database, so each test uses its own.
    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    [Fact]
    public async Task List_WithoutInactive_ExcludesThemAndOrdersByName()
    {
        var second = Procedure.Create(UniqueName("zz-b"), null, 30, 10m);
        var first = Procedure.Create(UniqueName("zz-a"), null, 30, 10m);
        var inactive = Procedure.Create(UniqueName("zz-c"), null, 30, 10m);
        inactive.Deactivate();
        fixture.Save(second, first, inactive);

        await using var context = fixture.CreateContext();
        var list = await new ProcedureRepository(context).ListAsync(includeInactive: false);

        var ids = list.Select(p => p.Id).ToList();
        Assert.DoesNotContain(inactive.Id, ids);
        Assert.True(ids.IndexOf(first.Id) < ids.IndexOf(second.Id));
        Assert.All(list, p => Assert.True(p.IsActive));
    }

    [Fact]
    public async Task List_WithInactive_IncludesThem()
    {
        var inactive = Procedure.Create(UniqueName("Inactive"), null, 30, 10m);
        inactive.Deactivate();
        fixture.Save(inactive);

        await using var context = fixture.CreateContext();
        var list = await new ProcedureRepository(context).ListAsync(includeInactive: true);

        Assert.Contains(list, p => p.Id == inactive.Id);
    }

    [Theory]
    [InlineData("Cleaning")]
    [InlineData("cleaning")]
    [InlineData("CLEANING")]
    public async Task NameExists_IgnoresCase(string name)
    {
        // The fixture already saved a procedure named "Cleaning".
        await using var context = fixture.CreateContext();

        Assert.True(await new ProcedureRepository(context).NameExistsAsync(name, excludingId: null));
    }

    [Fact]
    public async Task NameExists_IgnoresTheExcludedProcedure()
    {
        await using var context = fixture.CreateContext();
        var repository = new ProcedureRepository(context);

        Assert.False(await repository.NameExistsAsync("cleaning", fixture.SeedProcedure.Id));
        Assert.False(await repository.NameExistsAsync(UniqueName("Nobody"), excludingId: null));
    }

    [Fact]
    public async Task GetById_ReturnsATrackedEntity_SoChangesAreSavedByTheUnitOfWork()
    {
        var procedure = Procedure.Create(UniqueName("Resina"), null, 45, 40m);
        fixture.Save(procedure);

        await using (var context = fixture.CreateContext())
        {
            var repository = new ProcedureRepository(context);
            var loaded = await repository.GetByIdAsync(procedure.Id);

            Assert.NotNull(loaded);
            loaded.Deactivate();
            await context.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();
        Assert.False((await new ProcedureRepository(readContext).GetByIdAsync(procedure.Id))!.IsActive);
        Assert.Null(await new ProcedureRepository(readContext).GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Add_SavesNothingUntilTheUnitOfWorkSaves()
    {
        var procedure = Procedure.Create(UniqueName("Corona"), null, 60, 300m);

        await using var context = fixture.CreateContext();
        new ProcedureRepository(context).Add(procedure);

        await using (var before = fixture.CreateContext())
        {
            Assert.Null(await new ProcedureRepository(before).GetByIdAsync(procedure.Id));
        }

        await context.SaveChangesAsync();

        await using var after = fixture.CreateContext();
        Assert.NotNull(await new ProcedureRepository(after).GetByIdAsync(procedure.Id));
    }

    [Fact]
    public async Task Rn16_ChangingTheDuration_DoesNotChangeAnAppointmentAlreadyScheduled()
    {
        var procedure = Procedure.Create(UniqueName("Endodoncia"), null, 30, 100m);
        var (user, dentist) = NewDentist();
        var chair = NewChair();
        fixture.Save(procedure, user, dentist, chair);

        var appointment = Appointment.Schedule(
            fixture.SeedPatient, dentist, chair, procedure, MondayAt(9, 0), fixture.SeedUser.Id,
            new SchedulingContext(Ecuador, [], []));
        fixture.Save(appointment);

        await using (var context = fixture.CreateContext())
        {
            var loaded = await new ProcedureRepository(context).GetByIdAsync(procedure.Id);
            loaded!.Update(loaded.Name, loaded.Description, 90, loaded.BasePrice);
            await context.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();
        var stored = await readContext.Appointments.AsNoTracking().SingleAsync(a => a.Id == appointment.Id);

        Assert.Equal(MondayAt(9, 30), stored.Slot.End);
        Assert.Equal(TimeSpan.FromMinutes(30), stored.Slot.Duration);
    }
}
