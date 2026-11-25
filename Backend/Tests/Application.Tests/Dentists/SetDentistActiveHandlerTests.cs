using Application.Common;
using Application.Dentists;
using Application.Tests.Fakes;
using Domain.Common;
using Domain.Scheduling;
using Domain.Users;

namespace Application.Tests.Dentists;

/// <summary>Activate and deactivate a dentist with their user, with in-memory doubles. All data is fictitious.</summary>
public class SetDentistActiveHandlerTests
{
    private readonly FakeDentistRepository _dentists = new();
    private readonly FakeUserRepository _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private SetDentistActiveHandler SetActive => new(_dentists, _users, _unitOfWork);

    private (Dentist Dentist, User User) SeedDentist(bool active = true)
    {
        var user = _users.Seed(User.Create("carla.mora@citadent.test", "Carla Mora", "hash", UserRole.Dentist));
        var dentist = _dentists.Seed(Dentist.Create(user.Id, "Carla", "Mora", "LIC-123", "Ortodoncia"));
        dentist.SetWeeklySchedule([new WorkingHours(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0))]);

        if (!active)
        {
            dentist.Deactivate();
            user.Deactivate();
        }

        return (dentist, user);
    }

    [Fact]
    public async Task Deactivate_TurnsOffTheDentistAndTheirUserAndSavesOnce()
    {
        var (dentist, user) = SeedDentist();

        var result = await SetActive.HandleAsync(new SetDentistActiveCommand(dentist.Id, IsActive: false));

        Assert.False(result.IsActive);
        Assert.False(dentist.IsActive);
        Assert.False(user.IsActive);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Activate_TurnsOnTheDentistAndTheirUserAndSavesOnce()
    {
        var (dentist, user) = SeedDentist(active: false);

        var result = await SetActive.HandleAsync(new SetDentistActiveCommand(dentist.Id, IsActive: true));

        Assert.True(result.IsActive);
        Assert.True(dentist.IsActive);
        Assert.True(user.IsActive);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AskingForTheCurrentState_KeepsItWithoutError(bool active)
    {
        var (dentist, user) = SeedDentist(active);

        var result = await SetActive.HandleAsync(new SetDentistActiveCommand(dentist.Id, active));

        Assert.Equal(active, result.IsActive);
        Assert.Equal(active, user.IsActive);
    }

    [Fact]
    public async Task Deactivate_KeepsTheDataAndTheSchedule()
    {
        var (dentist, user) = SeedDentist();

        var result = await SetActive.HandleAsync(new SetDentistActiveCommand(dentist.Id, IsActive: false));

        Assert.Equal("Carla", result.FirstName);
        Assert.Equal("LIC-123", result.LicenseNumber);
        Assert.Single(result.WeeklySchedule);
        Assert.Equal("Carla Mora", user.FullName);
    }

    [Fact]
    public async Task AnUnknownDentist_ThrowsNotFoundAndSavesNothing()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => SetActive.HandleAsync(new SetDentistActiveCommand(Guid.NewGuid(), IsActive: false)));

        Assert.Equal(ApplicationErrorCodes.NotFound, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ADentistWithoutUser_IsBrokenDataAndChangesNothing()
    {
        var dentist = _dentists.Seed(Dentist.Create(Guid.NewGuid(), "Carla", "Mora", "LIC-123"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SetActive.HandleAsync(new SetDentistActiveCommand(dentist.Id, IsActive: false)));

        Assert.True(dentist.IsActive);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }
}
