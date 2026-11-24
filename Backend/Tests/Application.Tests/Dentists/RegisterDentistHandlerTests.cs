using Application.Common;
using Application.Dentists;
using Application.Tests.Fakes;
using Domain.Common;
using Domain.Scheduling;
using Domain.Users;

namespace Application.Tests.Dentists;

/// <summary>Register a dentist with their user account, with in-memory doubles. All data is fictitious.</summary>
public class RegisterDentistHandlerTests
{
    private const string Password = "una-clave-larga-1";

    private readonly FakeDentistRepository _dentists = new();
    private readonly FakeUserRepository _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private RegisterDentistHandler Register => new(_dentists, _users, new FakePasswordHasher(), _unitOfWork);

    private static RegisterDentistCommand Carla(string email = "carla.mora@citadent.test", string license = "LIC-123") =>
        new(email, Password, " Carla ", "Mora", license, "Ortodoncia");

    [Fact]
    public async Task Register_CreatesTheDentistAndTheirUserAndSavesOnce()
    {
        var result = await Register.HandleAsync(Carla());

        var user = Assert.Single(_users.All);
        var dentist = Assert.Single(_dentists.All);
        Assert.Equal(dentist.Id, result.Id);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(user.Id, dentist.UserId);
        Assert.Equal("Carla", result.FirstName);
        Assert.Equal("Mora", result.LastName);
        Assert.Equal("Ortodoncia", result.Specialty);
        Assert.Equal("LIC-123", result.LicenseNumber);
        Assert.True(result.IsActive);
        Assert.Empty(result.WeeklySchedule);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Register_TheUserIsAnActiveDentistWithAHashedPassword()
    {
        await Register.HandleAsync(Carla(" Carla.Mora@CitaDent.test "));

        var user = Assert.Single(_users.All);
        Assert.Equal(UserRole.Dentist, user.Role);
        Assert.True(user.IsActive);
        Assert.Equal("carla.mora@citadent.test", user.Email);
        Assert.Equal("Carla Mora", user.FullName);
        Assert.Equal(FakePasswordHasher.HashOf(Password), user.PasswordHash);
        Assert.NotEqual(Password, user.PasswordHash);
    }

    [Fact]
    public async Task Register_WithAnEmailAlreadyUsed_ThrowsDuplicateAndSavesNothing()
    {
        _users.Seed(User.Create("carla.mora@citadent.test", "Someone Else", "hash", UserRole.Reception));

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Register.HandleAsync(Carla("CARLA.MORA@citadent.test")));

        Assert.Equal(ApplicationErrorCodes.Duplicate, ex.Code);
        Assert.DoesNotContain("carla", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(_users.All);
        Assert.Empty(_dentists.All);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData("LIC-123")]
    [InlineData("lic-123")]
    [InlineData("  LIC-123 ")]
    public async Task Register_WithALicenseAlreadyUsed_ThrowsDuplicateAndSavesNothing(string license)
    {
        _dentists.Seed(Dentist.Create(Guid.NewGuid(), "Luis", "Vera", "LIC-123"));

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Register.HandleAsync(Carla(license: license)));

        Assert.Equal(ApplicationErrorCodes.Duplicate, ex.Code);
        Assert.Empty(_users.All);
        Assert.Single(_dentists.All);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("corta-11-ch")]
    public async Task Register_WithAShortPassword_ThrowsValidationAndSavesNothing(string password)
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Register.HandleAsync(Carla() with { Password = password }));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        Assert.Empty(_users.All);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Register_WithAPasswordOfTheMinimumLength_IsAllowed()
    {
        var password = new string('x', RegisterDentistHandler.MinimumPasswordLength);

        await Register.HandleAsync(Carla() with { Password = password });

        Assert.Single(_dentists.All);
    }

    [Theory]
    [InlineData("no-es-un-email")]
    [InlineData("")]
    public async Task Register_WithAnInvalidEmail_ThrowsValidation(string email)
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Register.HandleAsync(Carla(email)));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Register_WithoutFirstName_ThrowsValidation()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => Register.HandleAsync(Carla() with { FirstName = "  " }));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Register_WithoutLicense_ThrowsValidation()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => Register.HandleAsync(Carla(license: " ")));

        Assert.Equal(DomainException.ValidationCode, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Register_WithTheLongestNames_CutsTheUsersFullName()
    {
        var name = new string('A', Dentist.NameMaxLength);

        await Register.HandleAsync(Carla() with { FirstName = name, LastName = name });

        Assert.Equal(User.FullNameMaxLength, Assert.Single(_users.All).FullName.Length);
        Assert.Equal(name, Assert.Single(_dentists.All).LastName);
    }
}
