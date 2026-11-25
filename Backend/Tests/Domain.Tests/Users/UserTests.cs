using Domain.Common;
using Domain.Users;
using Xunit;

namespace Domain.Tests.Users;

public class UserTests
{
    private const string Hash = "hashed-password";

    [Fact]
    public void Create_ReturnsAnActiveUser_WithNormalizedData()
    {
        var user = User.Create("  Ana.Torres@CitaDent.EC ", "  Ana Torres  ", Hash, UserRole.Reception);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("ana.torres@citadent.ec", user.Email);
        Assert.Equal("Ana Torres", user.FullName);
        Assert.Equal(Hash, user.PasswordHash);
        Assert.Equal(UserRole.Reception, user.Role);
        Assert.True(user.IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Throws_WhenTheEmailIsBlank(string? email)
    {
        var ex = Assert.Throws<DomainException>(() => User.Create(email!, "Ana Torres", Hash, UserRole.Admin));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("a@")]
    [InlineData("@b.com")]
    [InlineData("a b@c.com")]
    [InlineData("a@@b.com")]
    [InlineData("a@b@c.com")]
    [InlineData("a@localhost")]
    [InlineData("a@.com")]
    [InlineData("a@b.")]
    public void Create_Throws_WhenTheEmailFormatIsInvalid(string email)
    {
        var ex = Assert.Throws<DomainException>(() => User.Create(email, "Ana Torres", Hash, UserRole.Admin));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Fact]
    public void Create_Throws_WhenTheEmailIsTooLong()
    {
        var email = new string('a', User.EmailMaxLength - "@b.ec".Length + 1) + "@b.ec";

        var ex = Assert.Throws<DomainException>(() => User.Create(email, "Ana Torres", Hash, UserRole.Admin));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Throws_WhenTheFullNameIsBlank(string? fullName)
    {
        var ex = Assert.Throws<DomainException>(() => User.Create("ana@citadent.ec", fullName!, Hash, UserRole.Admin));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Fact]
    public void Create_Throws_WhenTheFullNameIsTooLong()
    {
        var fullName = new string('A', User.FullNameMaxLength + 1);

        var ex = Assert.Throws<DomainException>(() => User.Create("ana@citadent.ec", fullName, Hash, UserRole.Admin));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Throws_WhenThePasswordHashIsBlank(string? hash)
    {
        var ex = Assert.Throws<DomainException>(() => User.Create("ana@citadent.ec", "Ana Torres", hash!, UserRole.Admin));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Create_Throws_WhenTheRoleIsNotDefined(int role)
    {
        var ex = Assert.Throws<DomainException>(
            () => User.Create("ana@citadent.ec", "Ana Torres", Hash, (UserRole)role));

        Assert.Equal("VALIDATION", ex.Code);
    }

    [Fact]
    public void ChangeFullName_TrimsAndKeepsTheRestOfTheUser()
    {
        var user = User.Create("ana@citadent.ec", "Ana Torres", Hash, UserRole.Dentist);

        user.ChangeFullName("  Ana María Torres ");

        Assert.Equal("Ana María Torres", user.FullName);
        Assert.Equal("ana@citadent.ec", user.Email);
        Assert.Equal(Hash, user.PasswordHash);
        Assert.Equal(UserRole.Dentist, user.Role);
        Assert.True(user.IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TOO-LONG")]
    public void ChangeFullName_Throws_AndKeepsTheOldName_WhenTheNameIsInvalid(string? fullName)
    {
        var user = User.Create("ana@citadent.ec", "Ana Torres", Hash, UserRole.Dentist);
        var candidate = fullName == "TOO-LONG" ? new string('A', User.FullNameMaxLength + 1) : fullName;

        var ex = Assert.Throws<DomainException>(() => user.ChangeFullName(candidate!));

        Assert.Equal("VALIDATION", ex.Code);
        Assert.Equal("Ana Torres", user.FullName);
    }

    [Fact]
    public void ChangeEmail_NormalizesItAndKeepsTheRestOfTheUser()
    {
        var user = User.Create("ana@citadent.ec", "Ana Torres", Hash, UserRole.Reception);

        user.ChangeEmail("  Ana.Torres@CitaDent.EC ");

        Assert.Equal("ana.torres@citadent.ec", user.Email);
        Assert.Equal("Ana Torres", user.FullName);
        Assert.Equal(Hash, user.PasswordHash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("no-es-email")]
    [InlineData("ana@citadent")]
    [InlineData("ana @citadent.ec")]
    public void ChangeEmail_Throws_AndKeepsTheOldEmail_WhenItIsInvalid(string? email)
    {
        var user = User.Create("ana@citadent.ec", "Ana Torres", Hash, UserRole.Reception);

        var ex = Assert.Throws<DomainException>(() => user.ChangeEmail(email!));

        Assert.Equal("VALIDATION", ex.Code);
        Assert.Equal("ana@citadent.ec", user.Email);
    }

    [Fact]
    public void ChangePasswordHash_ReplacesTheHash()
    {
        var user = User.Create("ana@citadent.ec", "Ana Torres", Hash, UserRole.Reception);

        user.ChangePasswordHash("new-hash");

        Assert.Equal("new-hash", user.PasswordHash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangePasswordHash_Throws_AndKeepsTheOldHash_WhenItIsBlank(string? hash)
    {
        var user = User.Create("ana@citadent.ec", "Ana Torres", Hash, UserRole.Reception);

        var ex = Assert.Throws<DomainException>(() => user.ChangePasswordHash(hash!));

        Assert.Equal("VALIDATION", ex.Code);
        Assert.Equal(Hash, user.PasswordHash);
    }

    [Theory]
    [InlineData(UserRole.Reception, UserRole.Admin)]
    [InlineData(UserRole.Admin, UserRole.Reception)]
    [InlineData(UserRole.Admin, UserRole.Admin)]
    public void ChangeRole_BetweenAdminAndReception_IsAllowed(UserRole from, UserRole to)
    {
        var user = User.Create("ana@citadent.ec", "Ana Torres", Hash, from);

        user.ChangeRole(to);

        Assert.Equal(to, user.Role);
    }

    [Theory]
    [InlineData(UserRole.Reception, UserRole.Dentist)]
    [InlineData(UserRole.Admin, UserRole.Dentist)]
    [InlineData(UserRole.Dentist, UserRole.Admin)]
    [InlineData(UserRole.Dentist, UserRole.Reception)]
    [InlineData(UserRole.Reception, (UserRole)0)]
    [InlineData(UserRole.Reception, (UserRole)99)]
    public void ChangeRole_Throws_AndKeepsTheOldRole_ToOrFromDentistOrAnUnknownRole(UserRole from, UserRole to)
    {
        var user = User.Create("ana@citadent.ec", "Ana Torres", Hash, from);

        var ex = Assert.Throws<DomainException>(() => user.ChangeRole(to));

        Assert.Equal("VALIDATION", ex.Code);
        Assert.Equal(from, user.Role);
    }

    [Fact]
    public void DeactivateAndActivate_ToggleTheState()
    {
        var user = User.Create("ana@citadent.ec", "Ana Torres", Hash, UserRole.Dentist);

        user.Deactivate();
        Assert.False(user.IsActive);

        user.Activate();
        Assert.True(user.IsActive);
    }
}
