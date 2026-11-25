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
