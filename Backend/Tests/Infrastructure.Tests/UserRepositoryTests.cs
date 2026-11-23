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
}
