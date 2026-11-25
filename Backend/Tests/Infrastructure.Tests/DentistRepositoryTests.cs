using Domain.Scheduling;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using static Infrastructure.Tests.TestData;

namespace Infrastructure.Tests;

/// <summary>All data is fictitious.</summary>
public class DentistRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static WorkingHours Block(DayOfWeek day, int startHour, int endHour) =>
        new(day, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0));

    // A dentist saved with its user and the TestData schedule (Monday 08-12 and 14-18).
    private Dentist SaveDentist(string lastName = "Mora", bool active = true)
    {
        var user = NewUser(Domain.Users.UserRole.Dentist);
        var dentist = Dentist.Create(user.Id, "Carla", lastName, $"LIC-{Guid.NewGuid():N}"[..20]);
        dentist.SetWeeklySchedule([Block(DayOfWeek.Monday, 8, 12), Block(DayOfWeek.Monday, 14, 18)]);

        if (!active)
        {
            dentist.Deactivate();
        }

        fixture.Save(user, dentist);
        return dentist;
    }

    // Reads the table directly, so the check does not depend on how EF loads the owned collection.
    private async Task<List<(DayOfWeek Day, TimeOnly Start, TimeOnly End)>> ScheduleRowsAsync(Guid dentistId)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "select day_of_week, start_time, end_time from dentist_schedules where dentist_id = @id " +
            "order by day_of_week, start_time",
            connection);
        command.Parameters.AddWithValue("id", dentistId);

        var rows = new List<(DayOfWeek, TimeOnly, TimeOnly)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(((DayOfWeek)reader.GetInt16(0), reader.GetFieldValue<TimeOnly>(1), reader.GetFieldValue<TimeOnly>(2)));
        }

        return rows;
    }

    private async Task ReplaceScheduleAsync(Guid dentistId, params WorkingHours[] hours)
    {
        await using var context = fixture.CreateContext();
        var tracked = await new DentistRepository(context).GetByIdAsync(dentistId);
        tracked!.SetWeeklySchedule(hours);
        await context.SaveChangesAsync();
    }

    // Get

    [Fact]
    public async Task GetById_LoadsTheWeeklySchedule()
    {
        var dentist = SaveDentist();

        await using var context = fixture.CreateContext();
        var found = await new DentistRepository(context).GetByIdAsync(dentist.Id);

        Assert.NotNull(found);
        Assert.Equal(dentist.UserId, found.UserId);
        Assert.Equal(2, found.WeeklySchedule.Count);
        Assert.Contains(Block(DayOfWeek.Monday, 14, 18), found.WeeklySchedule);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNull()
    {
        await using var context = fixture.CreateContext();

        Assert.Null(await new DentistRepository(context).GetByIdAsync(Guid.NewGuid()));
    }

    // Weekly schedule: replaced as a whole

    [Fact]
    public async Task SetWeeklySchedule_ReplacesTheRowsInTheDatabase()
    {
        var dentist = SaveDentist();

        await ReplaceScheduleAsync(dentist.Id, Block(DayOfWeek.Wednesday, 9, 13));

        var rows = await ScheduleRowsAsync(dentist.Id);
        Assert.Equal([(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(13, 0))], rows);
    }

    [Fact]
    public async Task SetWeeklySchedule_KeepingABlockThatAlreadyExisted_LeavesNoDuplicates()
    {
        // Monday 08-12 is in the old and in the new schedule: the same values, but a new object.
        var dentist = SaveDentist();

        await ReplaceScheduleAsync(dentist.Id, Block(DayOfWeek.Monday, 8, 12), Block(DayOfWeek.Tuesday, 8, 12));

        var rows = await ScheduleRowsAsync(dentist.Id);
        Assert.Equal(
            [
                (DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
                (DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(12, 0))
            ],
            rows);
    }

    [Fact]
    public async Task SetWeeklySchedule_WithAnEmptyList_DeletesEveryRow()
    {
        var dentist = SaveDentist();

        await ReplaceScheduleAsync(dentist.Id);

        Assert.Empty(await ScheduleRowsAsync(dentist.Id));
    }

    [Fact]
    public async Task SetWeeklySchedule_DoesNotTouchOtherDentists()
    {
        var dentist = SaveDentist();
        var other = SaveDentist();

        await ReplaceScheduleAsync(dentist.Id);

        Assert.Equal(2, (await ScheduleRowsAsync(other.Id)).Count);
    }

    [Fact]
    public async Task SetWeeklySchedule_ReplacedTwice_ReadsBackTheLastOne()
    {
        var dentist = SaveDentist();
        await ReplaceScheduleAsync(dentist.Id, Block(DayOfWeek.Friday, 8, 12));
        await ReplaceScheduleAsync(dentist.Id, Block(DayOfWeek.Saturday, 9, 11), Block(DayOfWeek.Saturday, 11, 13));

        await using var context = fixture.CreateContext();
        var found = await new DentistRepository(context).GetByIdAsync(dentist.Id);

        Assert.Equal(2, found!.WeeklySchedule.Count);
        Assert.All(found.WeeklySchedule, h => Assert.Equal(DayOfWeek.Saturday, h.Day));
    }

    // List

    [Fact]
    public async Task List_WithoutInactive_ExcludesThemAndOrdersByLastName()
    {
        var mark = Guid.NewGuid().ToString("N")[..8];
        var second = SaveDentist($"zz{mark}b");
        var first = SaveDentist($"zz{mark}a");
        var inactive = SaveDentist($"zz{mark}c", active: false);

        await using var context = fixture.CreateContext();
        var list = await new DentistRepository(context).ListAsync(includeInactive: false);

        var ids = list.Select(d => d.Id).ToList();
        Assert.DoesNotContain(inactive.Id, ids);
        Assert.True(ids.IndexOf(first.Id) < ids.IndexOf(second.Id));
        Assert.All(list, d => Assert.True(d.IsActive));
    }

    [Fact]
    public async Task List_WithInactive_IncludesThemWithTheirSchedules()
    {
        var inactive = SaveDentist(active: false);

        await using var context = fixture.CreateContext();
        var list = await new DentistRepository(context).ListAsync(includeInactive: true);

        Assert.Equal(2, list.Single(d => d.Id == inactive.Id).WeeklySchedule.Count);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    // License number

    [Fact]
    public async Task LicenseNumberExists_IgnoresCase()
    {
        var dentist = SaveDentist();

        await using var context = fixture.CreateContext();
        var repository = new DentistRepository(context);

        Assert.True(await repository.LicenseNumberExistsAsync(dentist.LicenseNumber, excludingId: null));
        Assert.True(await repository.LicenseNumberExistsAsync(dentist.LicenseNumber.ToLowerInvariant(), excludingId: null));
        Assert.False(await repository.LicenseNumberExistsAsync($"LIC-{Guid.NewGuid():N}"[..20], excludingId: null));
    }

    [Fact]
    public async Task LicenseNumberExists_IgnoresTheExcludedDentist_ButNotTheOthers()
    {
        var dentist = SaveDentist();
        var other = SaveDentist();

        await using var context = fixture.CreateContext();
        var repository = new DentistRepository(context);

        Assert.False(await repository.LicenseNumberExistsAsync(dentist.LicenseNumber.ToLowerInvariant(), dentist.Id));
        Assert.True(await repository.LicenseNumberExistsAsync(other.LicenseNumber, dentist.Id));
    }

    [Fact]
    public async Task Update_ChangedThroughATrackedDentist_IsSaved()
    {
        var dentist = SaveDentist();
        var newLicense = $"LIC-{Guid.NewGuid():N}"[..20];

        await using (var context = fixture.CreateContext())
        {
            var tracked = await new DentistRepository(context).GetByIdAsync(dentist.Id);
            tracked!.Update("Luisa", "Vera", newLicense, specialty: null);
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateContext())
        {
            var reloaded = await new DentistRepository(context).GetByIdAsync(dentist.Id);
            Assert.Equal("Luisa", reloaded!.FirstName);
            Assert.Equal("Vera", reloaded.LastName);
            Assert.Equal(newLicense, reloaded.LicenseNumber);
            Assert.Null(reloaded.Specialty);
            Assert.Equal(2, reloaded.WeeklySchedule.Count);
        }
    }

    [Fact]
    public void TheDatabaseRejects_TwoDentistsWithTheSameLicense()
    {
        var dentist = SaveDentist();
        var user = NewUser(Domain.Users.UserRole.Dentist);
        var copy = Dentist.Create(user.Id, "Luis", "Vera", dentist.LicenseNumber);

        var ex = Assert.Throws<DbUpdateException>(() => fixture.Save(user, copy));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState);
    }

    // Add

    [Fact]
    public async Task Add_WithItsUser_WritesBothOnSave()
    {
        var user = NewUser(Domain.Users.UserRole.Dentist);
        var dentist = Dentist.Create(user.Id, "Carla", "Mora", $"LIC-{Guid.NewGuid():N}"[..20]);

        await using (var context = fixture.CreateContext())
        {
            // The dentist is added first on purpose: EF orders the inserts by the foreign key.
            new DentistRepository(context).Add(dentist);
            new UserRepository(context).Add(user);
            await context.SaveChangesAsync();
        }

        await using var after = fixture.CreateContext();
        Assert.NotNull(await new DentistRepository(after).GetByIdAsync(dentist.Id));
        Assert.NotNull(await new UserRepository(after).FindByEmailAsync(user.Email));
    }
}
