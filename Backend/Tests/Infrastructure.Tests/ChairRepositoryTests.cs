using Domain.Scheduling;
using Infrastructure.Persistence.Repositories;
using Xunit;

namespace Infrastructure.Tests;

public class ChairRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // Names are unique in the database, so each test uses its own (at most 50 characters).
    // ASCII only: how lower() folds accented letters depends on the database locale.
    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private Chair SaveChair(string name, bool active = true)
    {
        var chair = Chair.Create(name);

        if (!active)
        {
            chair.Deactivate();
        }

        fixture.Save(chair);
        return chair;
    }

    [Fact]
    public async Task List_WithoutInactive_ExcludesThemAndOrdersByName()
    {
        var second = SaveChair(UniqueName("zz-b"));
        var first = SaveChair(UniqueName("zz-a"));
        var inactive = SaveChair(UniqueName("zz-c"), active: false);

        await using var context = fixture.CreateContext();
        var list = await new ChairRepository(context).ListAsync(includeInactive: false);

        var ids = list.Select(c => c.Id).ToList();
        Assert.DoesNotContain(inactive.Id, ids);
        Assert.True(ids.IndexOf(first.Id) < ids.IndexOf(second.Id));
        Assert.All(list, c => Assert.True(c.IsActive));
    }

    [Fact]
    public async Task List_WithInactive_IncludesThem()
    {
        var inactive = SaveChair(UniqueName("Inactive"), active: false);

        await using var context = fixture.CreateContext();
        var list = await new ChairRepository(context).ListAsync(includeInactive: true);

        Assert.Contains(list, c => c.Id == inactive.Id);
    }

    [Fact]
    public async Task List_ReturnsUntrackedChairs()
    {
        SaveChair(UniqueName("Untracked"));

        await using var context = fixture.CreateContext();
        await new ChairRepository(context).ListAsync(includeInactive: true);

        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task NameExists_IgnoresCase()
    {
        var name = UniqueName("Chair");
        SaveChair(name);

        await using var context = fixture.CreateContext();
        var repository = new ChairRepository(context);

        Assert.True(await repository.NameExistsAsync(name, excludingId: null));
        Assert.True(await repository.NameExistsAsync(name.ToUpperInvariant(), excludingId: null));
        Assert.False(await repository.NameExistsAsync(UniqueName("Nobody"), excludingId: null));
    }

    [Fact]
    public async Task NameExists_IgnoresTheExcludedChair()
    {
        var name = UniqueName("Chair");
        var chair = SaveChair(name);

        await using var context = fixture.CreateContext();
        var repository = new ChairRepository(context);

        Assert.False(await repository.NameExistsAsync(name.ToUpperInvariant(), chair.Id));
        Assert.True(await repository.NameExistsAsync(name, Guid.NewGuid()));
    }

    [Fact]
    public async Task NameExists_CountsInactiveChairs()
    {
        var name = UniqueName("Chair");
        SaveChair(name, active: false);

        await using var context = fixture.CreateContext();

        Assert.True(await new ChairRepository(context).NameExistsAsync(name, excludingId: null));
    }

    [Fact]
    public async Task GetById_ReturnsTheStoredChairOrNull()
    {
        var chair = SaveChair(UniqueName("Chair"));

        await using var context = fixture.CreateContext();
        var repository = new ChairRepository(context);

        var found = await repository.GetByIdAsync(chair.Id);

        Assert.NotNull(found);
        Assert.Equal(chair.Name, found.Name);
        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Add_WritesNothingUntilTheUnitOfWorkSaves()
    {
        var chair = Chair.Create(UniqueName("New"));

        await using var context = fixture.CreateContext();
        new ChairRepository(context).Add(chair);

        await using (var before = fixture.CreateContext())
        {
            Assert.Null(await new ChairRepository(before).GetByIdAsync(chair.Id));
        }

        await context.SaveChangesAsync();

        await using var after = fixture.CreateContext();
        var stored = await new ChairRepository(after).GetByIdAsync(chair.Id);
        Assert.NotNull(stored);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task ChangesToATrackedChair_AreSavedByTheUnitOfWork()
    {
        var chair = SaveChair(UniqueName("Old"));
        var newName = UniqueName("Renamed");

        await using (var context = fixture.CreateContext())
        {
            var tracked = await new ChairRepository(context).GetByIdAsync(chair.Id);
            tracked!.Rename(newName);
            tracked.Deactivate();
            await context.SaveChangesAsync();
        }

        await using var after = fixture.CreateContext();
        var stored = await new ChairRepository(after).GetByIdAsync(chair.Id);
        Assert.Equal(newName, stored!.Name);
        Assert.False(stored.IsActive);
    }
}
