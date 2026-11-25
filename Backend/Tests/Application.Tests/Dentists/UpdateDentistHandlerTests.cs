using Application.Common;
using Application.Dentists;
using Application.Tests.Fakes;
using Domain.Common;
using Domain.Scheduling;
using Domain.Users;

namespace Application.Tests.Dentists;

/// <summary>Edit a dentist's data and their user's full name, with in-memory doubles. All data is fictitious.</summary>
public class UpdateDentistHandlerTests
{
    private readonly FakeDentistRepository _dentists = new();
    private readonly FakeUserRepository _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private UpdateDentistHandler Update => new(_dentists, _users, _unitOfWork);

    private (Dentist Dentist, User User) SeedDentist(string license = "LIC-123")
    {
        var user = _users.Seed(User.Create($"{license.ToLowerInvariant()}@citadent.test", "Carla Mora", "hash", UserRole.Dentist));
        var dentist = _dentists.Seed(Dentist.Create(user.Id, "Carla", "Mora", license, "Ortodoncia"));

        return (dentist, user);
    }

    [Fact]
    public async Task Update_ChangesTheDentistAndTheUserFullNameAndSavesOnce()
    {
        var (dentist, user) = SeedDentist();

        var result = await Update.HandleAsync(
            new UpdateDentistCommand(dentist.Id, " Luisa ", " Vera ", " LIC-999 ", " Endodoncia "));

        Assert.Equal(dentist.Id, result.Id);
        Assert.Equal("Luisa", result.FirstName);
        Assert.Equal("Vera", result.LastName);
        Assert.Equal("LIC-999", result.LicenseNumber);
        Assert.Equal("Endodoncia", result.Specialty);
        Assert.Equal("Luisa Vera", user.FullName);
        Assert.Equal("lic-123@citadent.test", user.Email);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_WithABlankSpecialty_ClearsIt()
    {
        var (dentist, _) = SeedDentist();

        var result = await Update.HandleAsync(new UpdateDentistCommand(dentist.Id, "Carla", "Mora", "LIC-123", null));

        Assert.Null(result.Specialty);
        Assert.Null(dentist.Specialty);
    }

    [Fact]
    public async Task Update_WithLongNames_CutsTheUserFullNameToItsMaximum()
    {
        var (dentist, user) = SeedDentist();
        var firstName = new string('A', Dentist.NameMaxLength);
        var lastName = new string('B', Dentist.NameMaxLength);

        await Update.HandleAsync(new UpdateDentistCommand(dentist.Id, firstName, lastName, "LIC-123"));

        Assert.Equal(User.FullNameMaxLength, user.FullName.Length);
        Assert.StartsWith($"{firstName} B", user.FullName, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("LIC-123")]
    [InlineData("lic-123")]
    [InlineData(" Lic-123 ")]
    public async Task Update_KeepingItsOwnLicenseInAnyCase_IsAllowed(string license)
    {
        var (dentist, _) = SeedDentist();

        var result = await Update.HandleAsync(new UpdateDentistCommand(dentist.Id, "Carla", "Mora", license));

        Assert.Equal(license.Trim(), result.LicenseNumber);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData("LIC-456")]
    [InlineData("lic-456")]
    [InlineData("  LIC-456 ")]
    public async Task Update_WithTheLicenseOfAnotherDentist_ThrowsDuplicateAndSavesNothing(string license)
    {
        var (dentist, user) = SeedDentist();
        SeedDentist("LIC-456");

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Update.HandleAsync(new UpdateDentistCommand(dentist.Id, "Luisa", "Vera", license)));

        Assert.Equal(ApplicationErrorCodes.Duplicate, ex.Code);
        Assert.Equal("Carla Mora", user.FullName);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData("", "Vera", "LIC-999")]
    [InlineData("Luisa", " ", "LIC-999")]
    [InlineData("Luisa", "Vera", "")]
    public async Task Update_WithInvalidData_ThrowsValidationAndChangesNothing(
        string firstName, string lastName, string license)
    {
        var (dentist, user) = SeedDentist();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Update.HandleAsync(new UpdateDentistCommand(dentist.Id, firstName, lastName, license)));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        Assert.Equal("Carla", dentist.FirstName);
        Assert.Equal("LIC-123", dentist.LicenseNumber);
        Assert.Equal("Carla Mora", user.FullName);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_AnUnknownDentist_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Update.HandleAsync(new UpdateDentistCommand(Guid.NewGuid(), "Luisa", "Vera", "LIC-999")));

        Assert.Equal(ApplicationErrorCodes.NotFound, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_ADentistWithoutUser_IsBrokenDataAndSavesNothing()
    {
        var dentist = _dentists.Seed(Dentist.Create(Guid.NewGuid(), "Carla", "Mora", "LIC-123"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Update.HandleAsync(new UpdateDentistCommand(dentist.Id, "Luisa", "Vera", "LIC-999")));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_KeepsTheStateAndTheSchedule()
    {
        var (dentist, _) = SeedDentist();
        dentist.SetWeeklySchedule([new WorkingHours(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0))]);
        dentist.Deactivate();

        var result = await Update.HandleAsync(new UpdateDentistCommand(dentist.Id, "Luisa", "Vera", "LIC-999"));

        Assert.False(result.IsActive);
        Assert.Single(result.WeeklySchedule);
    }
}
