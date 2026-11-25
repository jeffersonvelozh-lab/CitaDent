using Application.Common;
using Application.Tests.Fakes;
using Application.Users;
using Domain.Common;
using Domain.Users;

namespace Application.Tests.Users;

/// <summary>Get and list users, with in-memory doubles. All data is fictitious.</summary>
public class UserQueryHandlersTests
{
    private readonly FakeUserRepository _users = new();

    private GetUserHandler Get => new(_users);
    private ListUsersHandler List => new(_users);

    private User Seed(string fullName, UserRole role, bool active = true)
    {
        var user = _users.Seed(User.Create($"{Guid.NewGuid():N}@citadent.test", fullName, "secret-hash", role));

        if (!active)
        {
            user.Deactivate();
        }

        return user;
    }

    [Fact]
    public async Task Get_ReturnsTheUserWithoutThePasswordHash()
    {
        var user = Seed("Ana Torres", UserRole.Reception);

        var result = await Get.HandleAsync(new GetUserQuery(user.Id));

        Assert.Equal(new UserDto(user.Id, user.Email, "Ana Torres", UserRole.Reception, true), result);
        Assert.DoesNotContain("secret-hash", result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_AnUnknownUser_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Get.HandleAsync(new GetUserQuery(Guid.NewGuid())));

        Assert.Equal(ApplicationErrorCodes.NotFound, ex.Code);
    }

    [Fact]
    public async Task List_ReturnsActiveUsersOfEveryRole_OrderedByName()
    {
        Seed("Luis Vera", UserRole.Admin);
        Seed("Ana Torres", UserRole.Reception);
        Seed("Carla Mora", UserRole.Dentist);
        Seed("Bruno Paz", UserRole.Reception, active: false);

        var result = await List.HandleAsync(new ListUsersQuery());

        Assert.Equal(["Ana Torres", "Carla Mora", "Luis Vera"], result.Select(u => u.FullName));
    }

    [Fact]
    public async Task List_WithInactive_IncludesThem()
    {
        Seed("Ana Torres", UserRole.Reception);
        Seed("Bruno Paz", UserRole.Reception, active: false);

        var result = await List.HandleAsync(new ListUsersQuery(IncludeInactive: true));

        Assert.Equal(["Ana Torres", "Bruno Paz"], result.Select(u => u.FullName));
        Assert.False(result[1].IsActive);
    }

    [Fact]
    public async Task List_WithARole_OnlyReturnsThatRole()
    {
        Seed("Luis Vera", UserRole.Admin);
        Seed("Ana Torres", UserRole.Reception);
        Seed("Bruno Paz", UserRole.Reception, active: false);

        var result = await List.HandleAsync(new ListUsersQuery(IncludeInactive: true, Role: UserRole.Reception));

        Assert.Equal(["Ana Torres", "Bruno Paz"], result.Select(u => u.FullName));
    }
}
