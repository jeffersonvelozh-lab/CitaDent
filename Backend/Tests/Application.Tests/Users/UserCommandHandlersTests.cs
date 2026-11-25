using Application.Common;
using Application.Tests.Fakes;
using Application.Users;
using Domain.Common;
using Domain.Users;

namespace Application.Tests.Users;

/// <summary>
/// Create users, change their email, password, role and state, with in-memory doubles. All data is fictitious.
/// </summary>
public class UserCommandHandlersTests
{
    private const string Password = "una-clave-larga-1";
    private const string NewPassword = "otra-clave-larga-2";

    private readonly FakeUserRepository _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private CreateUserHandler Create => new(_users, new FakePasswordHasher(), _unitOfWork);
    private ChangeUserEmailHandler ChangeEmail => new(_users, _unitOfWork);
    private ResetUserPasswordHandler ResetPassword => new(_users, new FakePasswordHasher(), _unitOfWork);
    private ChangeUserRoleHandler ChangeRole => new(_users, _unitOfWork);
    private SetUserActiveHandler SetActive => new(_users, _unitOfWork);
    private ChangeUserFullNameHandler ChangeFullName => new(_users, _unitOfWork);

    private User Seed(UserRole role, string? email = null, bool active = true)
    {
        var user = _users.Seed(User.Create(
            email ?? $"{Guid.NewGuid():N}@citadent.test", "Ana Torres", FakePasswordHasher.HashOf(Password), role));

        if (!active)
        {
            user.Deactivate();
        }

        return user;
    }

    private async Task AssertFailsWithoutSavingAsync(string code, Func<Task> action)
    {
        var ex = await Assert.ThrowsAsync<DomainException>(action);

        Assert.Equal(code, ex.Code);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // Create

    [Theory]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Admin)]
    public async Task Create_AddsAnActiveUserWithAHashedPasswordAndSavesOnce(UserRole role)
    {
        var result = await Create.HandleAsync(
            new CreateUserCommand(" Ana.Torres@CitaDent.test ", " Ana Torres ", Password, role));

        var user = Assert.Single(_users.All);
        Assert.Equal(new UserDto(user.Id, "ana.torres@citadent.test", "Ana Torres", role, true), result);
        Assert.Equal(FakePasswordHasher.HashOf(Password), user.PasswordHash);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_ADentistAccount_ThrowsValidation()
    {
        await AssertFailsWithoutSavingAsync(DomainException.ValidationCode, () =>
            Create.HandleAsync(new CreateUserCommand("carla@citadent.test", "Carla Mora", Password, UserRole.Dentist)));

        Assert.Empty(_users.All);
    }

    [Theory]
    [InlineData("")]
    [InlineData("corta-11-ch")]
    public async Task Create_WithAShortPassword_ThrowsValidation(string password)
    {
        await AssertFailsWithoutSavingAsync(DomainException.ValidationCode, () =>
            Create.HandleAsync(new CreateUserCommand("ana@citadent.test", "Ana Torres", password, UserRole.Reception)));
    }

    [Theory]
    [InlineData("no-es-email", "Ana Torres", UserRole.Reception)]
    [InlineData("ana@citadent.test", " ", UserRole.Reception)]
    [InlineData("ana@citadent.test", "Ana Torres", (UserRole)99)]
    public async Task Create_WithInvalidData_ThrowsValidation(string email, string fullName, UserRole role)
    {
        await AssertFailsWithoutSavingAsync(DomainException.ValidationCode, () =>
            Create.HandleAsync(new CreateUserCommand(email, fullName, Password, role)));
    }

    [Fact]
    public async Task Create_WithAnEmailAlreadyUsed_ThrowsDuplicateWithoutTheEmail()
    {
        Seed(UserRole.Dentist, "ana.torres@citadent.test");

        var ex = await Assert.ThrowsAsync<DomainException>(() => Create.HandleAsync(
            new CreateUserCommand("ANA.TORRES@citadent.test", "Ana Torres", Password, UserRole.Reception)));

        Assert.Equal(ApplicationErrorCodes.Duplicate, ex.Code);
        Assert.DoesNotContain("ana", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(_users.All);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // Change email

    [Fact]
    public async Task ChangeEmail_NormalizesAndSavesOnce()
    {
        var user = Seed(UserRole.Dentist);

        var result = await ChangeEmail.HandleAsync(new ChangeUserEmailCommand(user.Id, " Nuevo@CitaDent.test "));

        Assert.Equal("nuevo@citadent.test", result.Email);
        Assert.Equal("nuevo@citadent.test", user.Email);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ChangeEmail_ToItsOwnEmailInOtherCase_IsAllowed()
    {
        var user = Seed(UserRole.Reception, "ana@citadent.test");

        var result = await ChangeEmail.HandleAsync(new ChangeUserEmailCommand(user.Id, "ANA@citadent.test"));

        Assert.Equal("ana@citadent.test", result.Email);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ChangeEmail_ToTheEmailOfAnotherUser_ThrowsDuplicate()
    {
        Seed(UserRole.Admin, "luis@citadent.test");
        var user = Seed(UserRole.Reception, "ana@citadent.test");

        await AssertFailsWithoutSavingAsync(ApplicationErrorCodes.Duplicate, () =>
            ChangeEmail.HandleAsync(new ChangeUserEmailCommand(user.Id, "Luis@citadent.test")));
    }

    [Fact]
    public async Task ChangeEmail_WithAnInvalidEmail_ThrowsValidationAndKeepsTheOldOne()
    {
        var user = Seed(UserRole.Reception, "ana@citadent.test");

        await AssertFailsWithoutSavingAsync(DomainException.ValidationCode, () =>
            ChangeEmail.HandleAsync(new ChangeUserEmailCommand(user.Id, "no-es-email")));

        Assert.Equal("ana@citadent.test", user.Email);
    }

    [Fact]
    public async Task ChangeEmail_OfAnUnknownUser_ThrowsNotFound()
    {
        await AssertFailsWithoutSavingAsync(ApplicationErrorCodes.NotFound, () =>
            ChangeEmail.HandleAsync(new ChangeUserEmailCommand(Guid.NewGuid(), "nuevo@citadent.test")));
    }

    // Full name

    [Theory]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Admin)]
    public async Task ChangeFullName_TrimsAndSavesOnce(UserRole role)
    {
        var user = Seed(role);

        var result = await ChangeFullName.HandleAsync(new ChangeUserFullNameCommand(user.Id, "  Ana María Torres "));

        Assert.Equal("Ana María Torres", result.FullName);
        Assert.Equal("Ana María Torres", user.FullName);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TOO-LONG")]
    public async Task ChangeFullName_WithAnInvalidName_ThrowsValidationAndKeepsTheOldOne(string fullName)
    {
        var user = Seed(UserRole.Reception);
        var candidate = fullName == "TOO-LONG" ? new string('A', User.FullNameMaxLength + 1) : fullName;

        await AssertFailsWithoutSavingAsync(DomainException.ValidationCode, () =>
            ChangeFullName.HandleAsync(new ChangeUserFullNameCommand(user.Id, candidate)));

        Assert.Equal("Ana Torres", user.FullName);
    }

    [Fact]
    public async Task ChangeFullName_OfADentistAccount_ThrowsValidationAndKeepsTheName()
    {
        var user = Seed(UserRole.Dentist);

        await AssertFailsWithoutSavingAsync(DomainException.ValidationCode, () =>
            ChangeFullName.HandleAsync(new ChangeUserFullNameCommand(user.Id, "Otro Nombre")));

        Assert.Equal("Ana Torres", user.FullName);
    }

    [Fact]
    public async Task ChangeFullName_OfAnUnknownUser_ThrowsNotFound()
    {
        await AssertFailsWithoutSavingAsync(ApplicationErrorCodes.NotFound, () =>
            ChangeFullName.HandleAsync(new ChangeUserFullNameCommand(Guid.NewGuid(), "Ana Torres")));
    }

    // Reset password

    [Theory]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Dentist)]
    public async Task ResetPassword_StoresTheHashOfTheNewPasswordAndSavesOnce(UserRole role)
    {
        var user = Seed(role);

        await ResetPassword.HandleAsync(new ResetUserPasswordCommand(user.Id, NewPassword));

        Assert.Equal(FakePasswordHasher.HashOf(NewPassword), user.PasswordHash);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("corta-11-ch")]
    public async Task ResetPassword_WithAShortPassword_ThrowsValidationAndKeepsTheOldHash(string password)
    {
        var user = Seed(UserRole.Reception);

        await AssertFailsWithoutSavingAsync(DomainException.ValidationCode, () =>
            ResetPassword.HandleAsync(new ResetUserPasswordCommand(user.Id, password)));

        Assert.Equal(FakePasswordHasher.HashOf(Password), user.PasswordHash);
    }

    [Fact]
    public async Task ResetPassword_OfAnUnknownUser_ThrowsNotFound()
    {
        await AssertFailsWithoutSavingAsync(ApplicationErrorCodes.NotFound, () =>
            ResetPassword.HandleAsync(new ResetUserPasswordCommand(Guid.NewGuid(), NewPassword)));
    }

    // Change role

    [Fact]
    public async Task ChangeRole_PromotesReceptionToAdmin()
    {
        var user = Seed(UserRole.Reception);

        var result = await ChangeRole.HandleAsync(new ChangeUserRoleCommand(user.Id, UserRole.Admin));

        Assert.Equal(UserRole.Admin, result.Role);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ChangeRole_DemotesAnAdmin_WhenAnotherActiveAdminRemains()
    {
        Seed(UserRole.Admin);
        var user = Seed(UserRole.Admin);

        var result = await ChangeRole.HandleAsync(new ChangeUserRoleCommand(user.Id, UserRole.Reception));

        Assert.Equal(UserRole.Reception, result.Role);
    }

    [Fact]
    public async Task ChangeRole_DemotingTheLastActiveAdmin_ThrowsLastAdmin()
    {
        // An inactive admin does not count.
        Seed(UserRole.Admin, active: false);
        var user = Seed(UserRole.Admin);

        await AssertFailsWithoutSavingAsync(ApplicationErrorCodes.LastActiveAdmin, () =>
            ChangeRole.HandleAsync(new ChangeUserRoleCommand(user.Id, UserRole.Reception)));

        Assert.Equal(UserRole.Admin, user.Role);
    }

    [Fact]
    public async Task ChangeRole_KeepingTheLastActiveAdminAsAdmin_IsAllowed()
    {
        var user = Seed(UserRole.Admin);

        var result = await ChangeRole.HandleAsync(new ChangeUserRoleCommand(user.Id, UserRole.Admin));

        Assert.Equal(UserRole.Admin, result.Role);
    }

    [Fact]
    public async Task ChangeRole_DemotingAnInactiveAdmin_IsAllowed()
    {
        var user = Seed(UserRole.Admin, active: false);

        var result = await ChangeRole.HandleAsync(new ChangeUserRoleCommand(user.Id, UserRole.Reception));

        Assert.Equal(UserRole.Reception, result.Role);
    }

    [Theory]
    [InlineData(UserRole.Reception, UserRole.Dentist)]
    [InlineData(UserRole.Dentist, UserRole.Reception)]
    public async Task ChangeRole_ToOrFromDentist_ThrowsValidation(UserRole from, UserRole to)
    {
        Seed(UserRole.Admin);
        var user = Seed(from);

        await AssertFailsWithoutSavingAsync(DomainException.ValidationCode, () =>
            ChangeRole.HandleAsync(new ChangeUserRoleCommand(user.Id, to)));
    }

    [Fact]
    public async Task ChangeRole_OfAnUnknownUser_ThrowsNotFound()
    {
        await AssertFailsWithoutSavingAsync(ApplicationErrorCodes.NotFound, () =>
            ChangeRole.HandleAsync(new ChangeUserRoleCommand(Guid.NewGuid(), UserRole.Admin)));
    }

    // Activate and deactivate

    [Theory]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Admin)]
    public async Task Deactivate_TurnsTheUserOffAndSavesOnce(UserRole role)
    {
        Seed(UserRole.Admin);
        var user = Seed(role);

        var result = await SetActive.HandleAsync(new SetUserActiveCommand(user.Id, IsActive: false));

        Assert.False(result.IsActive);
        Assert.False(user.IsActive);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Activate_TurnsTheUserOnAndSavesOnce()
    {
        var user = Seed(UserRole.Reception, active: false);

        var result = await SetActive.HandleAsync(new SetUserActiveCommand(user.Id, IsActive: true));

        Assert.True(result.IsActive);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Deactivate_TheLastActiveAdmin_ThrowsLastAdmin()
    {
        Seed(UserRole.Admin, active: false);
        var user = Seed(UserRole.Admin);

        await AssertFailsWithoutSavingAsync(ApplicationErrorCodes.LastActiveAdmin, () =>
            SetActive.HandleAsync(new SetUserActiveCommand(user.Id, IsActive: false)));

        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task Deactivate_AnAlreadyInactiveAdmin_IsAllowed()
    {
        // No active admin at all: deactivating an inactive one does not make it worse.
        var user = Seed(UserRole.Admin, active: false);

        var result = await SetActive.HandleAsync(new SetUserActiveCommand(user.Id, IsActive: false));

        Assert.False(result.IsActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetActive_OnADentistAccount_ThrowsValidation(bool isActive)
    {
        var user = Seed(UserRole.Dentist, active: !isActive);

        await AssertFailsWithoutSavingAsync(DomainException.ValidationCode, () =>
            SetActive.HandleAsync(new SetUserActiveCommand(user.Id, isActive)));

        Assert.Equal(!isActive, user.IsActive);
    }

    [Fact]
    public async Task SetActive_OnAnUnknownUser_ThrowsNotFound()
    {
        await AssertFailsWithoutSavingAsync(ApplicationErrorCodes.NotFound, () =>
            SetActive.HandleAsync(new SetUserActiveCommand(Guid.NewGuid(), IsActive: false)));
    }
}
