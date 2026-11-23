using Infrastructure.Security;
using Xunit;

namespace Infrastructure.Tests.Security;

/// <summary>Unit tests: no database needed.</summary>
public class Pbkdf2PasswordHasherTests
{
    private const string Password = "Correct-Horse-42";

    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Verify_ReturnsTrue_ForTheCorrectPassword()
    {
        var hash = _hasher.Hash(Password);

        Assert.True(_hasher.Verify(Password, hash));
    }

    [Theory]
    [InlineData("wrong-password")]
    [InlineData("correct-Horse-42")]
    [InlineData("Correct-Horse-42 ")]
    public void Verify_ReturnsFalse_ForAWrongPassword(string attempt)
    {
        var hash = _hasher.Hash(Password);

        Assert.False(_hasher.Verify(attempt, hash));
    }

    [Fact]
    public void Hash_UsesADifferentSalt_EachTime()
    {
        var first = _hasher.Hash(Password);
        var second = _hasher.Hash(Password);

        Assert.NotEqual(first, second);
        Assert.NotEqual(first.Split('$')[2], second.Split('$')[2]);
        Assert.True(_hasher.Verify(Password, first));
        Assert.True(_hasher.Verify(Password, second));
    }

    [Fact]
    public void Hash_IsSelfDescribing_AndDoesNotContainThePassword()
    {
        var hash = _hasher.Hash(Password);
        var parts = hash.Split('$');

        Assert.Equal(4, parts.Length);
        Assert.Equal("PBKDF2-SHA256", parts[0]);
        Assert.Equal("600000", parts[1]);
        Assert.Equal(16, Convert.FromBase64String(parts[2]).Length);
        Assert.Equal(32, Convert.FromBase64String(parts[3]).Length);
        Assert.DoesNotContain(Password, hash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("BCRYPT$600000$c2FsdA==$aGFzaA==")]
    [InlineData("PBKDF2-SHA256$abc$c2FsdA==$aGFzaA==")]
    [InlineData("PBKDF2-SHA256$0$c2FsdA==$aGFzaA==")]
    [InlineData("PBKDF2-SHA256$-1$c2FsdA==$aGFzaA==")]
    [InlineData("PBKDF2-SHA256$1000$not base64!$aGFzaA==")]
    [InlineData("PBKDF2-SHA256$1000$c2FsdA==$")]
    [InlineData("PBKDF2-SHA256$1000$c2FsdA==$aGFzaA==$extra")]
    public void Verify_ReturnsFalse_ForAMalformedHash(string storedHash)
    {
        Assert.False(_hasher.Verify(Password, storedHash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForAnEmptyPassword()
    {
        var hash = _hasher.Hash(Password);

        Assert.False(_hasher.Verify("", hash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Hash_Throws_ForAMissingPassword(string? password)
    {
        Assert.ThrowsAny<ArgumentException>(() => _hasher.Hash(password!));
    }
}
