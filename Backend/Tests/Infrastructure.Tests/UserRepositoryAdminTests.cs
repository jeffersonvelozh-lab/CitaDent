using Domain.Users;
using Infrastructure.Persistence.Repositories;
using Xunit;
using static Infrastructure.Tests.TestData;

namespace Infrastructure.Tests;

/// <summary>
/// Counting active admins needs to know every admin in the database, so this class has its own database
/// (one per test class) and a single test that builds the situation step by step.
/// </summary>
public class UserRepositoryAdminTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private async Task<bool> AnyOtherActiveAdminAsync(Guid excludingId)
    {
        await using var context = fixture.CreateContext();
        return await new UserRepository(context).AnyOtherActiveAdminAsync(excludingId);
    }

    [Fact]
    public async Task AnyOtherActiveAdmin_CountsOnlyActiveAdminsOtherThanTheExcludedOne()
    {
        var admin = NewUser(UserRole.Admin);
        var reception = NewUser(UserRole.Reception);
        var inactiveAdmin = NewUser(UserRole.Admin);
        inactiveAdmin.Deactivate();
        fixture.Save(admin, reception, inactiveAdmin);

        // Only the admin itself, a receptionist and an inactive admin: no other active admin.
        Assert.False(await AnyOtherActiveAdminAsync(admin.Id));
        Assert.True(await AnyOtherActiveAdminAsync(reception.Id));

        var secondAdmin = NewUser(UserRole.Admin);
        fixture.Save(secondAdmin);

        Assert.True(await AnyOtherActiveAdminAsync(admin.Id));
        Assert.True(await AnyOtherActiveAdminAsync(secondAdmin.Id));
    }
}
