using Domain.Users;
using Infrastructure.Persistence.Repositories;
using Xunit;
using static Infrastructure.Tests.TestData;

namespace Infrastructure.Tests;

public class UserRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task FindByEmail_ReturnsTheUser_WhenItExists()
    {
        var user = NewUser(UserRole.Admin);
        fixture.Save(user);

        await using var context = fixture.CreateContext();
        var found = await new UserRepository(context).FindByEmailAsync(user.Email);

        Assert.NotNull(found);
        Assert.Equal(user.Id, found.Id);
        Assert.Equal(UserRole.Admin, found.Role);
    }

    [Fact]
    public async Task FindByEmail_ReturnsNull_WhenNoUserHasThatEmail()
    {
        await using var context = fixture.CreateContext();

        var found = await new UserRepository(context).FindByEmailAsync("nobody@citadent.test");

        Assert.Null(found);
    }

    [Fact]
    public async Task GetById_ReturnsATrackedUser_WhoseChangesAreSaved()
    {
        var user = NewUser(UserRole.Reception);
        fixture.Save(user);

        await using (var context = fixture.CreateContext())
        {
            var tracked = await new UserRepository(context).GetByIdAsync(user.Id);
            Assert.NotNull(tracked);
            tracked.ChangeFullName("Nombre Cambiado");
            await context.SaveChangesAsync();
        }

        var found = await FindWithAFreshContext(user.Email);
        Assert.Equal("Nombre Cambiado", found!.FullName);
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenNoUserHasThatId()
    {
        await using var context = fixture.CreateContext();

        Assert.Null(await new UserRepository(context).GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task EmailExists_SeesOtherUsers_ButNotTheExcludedOne()
    {
        var user = NewUser(UserRole.Reception);
        fixture.Save(user);

        await using var context = fixture.CreateContext();
        var repository = new UserRepository(context);

        Assert.True(await repository.EmailExistsAsync(user.Email, excludingId: null));
        Assert.True(await repository.EmailExistsAsync(user.Email, Guid.NewGuid()));
        Assert.False(await repository.EmailExistsAsync(user.Email, user.Id));
        Assert.False(await repository.EmailExistsAsync("nobody@citadent.test", excludingId: null));
    }

    [Fact]
    public async Task List_FiltersByStateAndRole_OrderedByNameAndIsNotTracked()
    {
        // The database is shared by the tests of this class, so only these users are looked at.
        var zoe = User.Create($"{Guid.NewGuid():N}@citadent.test", "Zoe Paz", "test-hash", UserRole.Reception);
        var ana = User.Create($"{Guid.NewGuid():N}@citadent.test", "Ana Torres", "test-hash", UserRole.Admin);
        var inactive = User.Create($"{Guid.NewGuid():N}@citadent.test", "Bruno Vera", "test-hash", UserRole.Reception);
        inactive.Deactivate();
        fixture.Save(zoe, ana, inactive);
        Guid[] mine = [zoe.Id, ana.Id, inactive.Id];

        await using var context = fixture.CreateContext();
        var repository = new UserRepository(context);

        var actives = await repository.ListAsync(includeInactive: false, role: null);
        var all = await repository.ListAsync(includeInactive: true, role: null);
        var reception = await repository.ListAsync(includeInactive: true, role: UserRole.Reception);

        Assert.Equal([ana.Id, zoe.Id], actives.Where(u => mine.Contains(u.Id)).Select(u => u.Id));
        Assert.Equal([ana.Id, inactive.Id, zoe.Id], all.Where(u => mine.Contains(u.Id)).Select(u => u.Id));
        Assert.All(reception, u => Assert.Equal(UserRole.Reception, u.Role));
        Assert.Equal([inactive.Id, zoe.Id], reception.Where(u => mine.Contains(u.Id)).Select(u => u.Id));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task EmailRoleAndPasswordChanges_ThroughATrackedUser_AreSaved()
    {
        var user = NewUser(UserRole.Reception);
        fixture.Save(user);
        var newEmail = $"changed-{Guid.NewGuid():N}@citadent.test";

        await using (var context = fixture.CreateContext())
        {
            var tracked = await new UserRepository(context).GetByIdAsync(user.Id);
            tracked!.ChangeEmail(newEmail);
            tracked.ChangeRole(UserRole.Admin);
            tracked.ChangePasswordHash("new-hash");
            tracked.Deactivate();
            await context.SaveChangesAsync();
        }

        Assert.Null(await FindWithAFreshContext(user.Email));
        var found = await FindWithAFreshContext(newEmail);
        Assert.NotNull(found);
        Assert.Equal(UserRole.Admin, found.Role);
        Assert.Equal("new-hash", found.PasswordHash);
        Assert.False(found.IsActive);
    }

    [Fact]
    public async Task AnyWithRole_IsFalseUntilAUserWithThatRoleIsSaved()
    {
        // No other test in this class saves a dentist, and each class has its own database.
        await using (var context = fixture.CreateContext())
        {
            Assert.False(await new UserRepository(context).AnyWithRoleAsync(UserRole.Dentist));
        }

        var dentist = NewUser(UserRole.Dentist);
        dentist.Deactivate();
        fixture.Save(dentist);

        await using (var context = fixture.CreateContext())
        {
            // Inactive users count too.
            Assert.True(await new UserRepository(context).AnyWithRoleAsync(UserRole.Dentist));
        }
    }

    [Fact]
    public async Task Add_WritesNothingUntilTheUnitOfWorkSaves()
    {
        var user = NewUser(UserRole.Admin);

        await using var context = fixture.CreateContext();
        new UserRepository(context).Add(user);

        Assert.Null(await FindWithAFreshContext(user.Email));

        // The DbContext is the unit of work (IUnitOfWork).
        await context.SaveChangesAsync();

        var found = await FindWithAFreshContext(user.Email);
        Assert.NotNull(found);
        Assert.Equal(user.Id, found.Id);
        Assert.Equal(user.PasswordHash, found.PasswordHash);
    }

    private async Task<User?> FindWithAFreshContext(string email)
    {
        await using var context = fixture.CreateContext();
        return await new UserRepository(context).FindByEmailAsync(email);
    }
}
