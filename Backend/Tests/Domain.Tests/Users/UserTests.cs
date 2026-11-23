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
    public void DeactivateAndActivate_ToggleTheState()
    {
        var user = User.Create("ana@citadent.ec", "Ana Torres", Hash, UserRole.Dentist);

        user.Deactivate();
        Assert.False(user.IsActive);

        user.Activate();
        Assert.True(user.IsActive);
    }
}
