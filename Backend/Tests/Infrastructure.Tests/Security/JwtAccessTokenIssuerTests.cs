using System.Text;
using Domain.Users;
using Infrastructure.Security;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Infrastructure.Tests.Security;

/// <summary>Unit tests: no database needed.</summary>
public class JwtAccessTokenIssuerTests
{
    private const string SigningKey = "test-signing-key-with-at-least-32-bytes!";

    private static readonly DateTimeOffset Now = new(2026, 12, 1, 14, 30, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Now);
    private readonly User _user = User.Create("ana@citadent.ec", "Ana Torres", "hash", UserRole.Dentist);

    private static JwtOptions Settings(string key = SigningKey, int minutes = 60) => new()
    {
        Issuer = "CitaDent",
        Audience = "CitaDent.Api",
        SigningKey = key,
        ExpirationMinutes = minutes
    };

    private JwtAccessTokenIssuer Issuer(JwtOptions? settings = null) =>
        new(Microsoft.Extensions.Options.Options.Create(settings ?? Settings()), _time);

    [Fact]
    public void Issue_PutsTheUserAndRoleInTheToken()
    {
        var token = new JsonWebToken(Issuer().Issue(_user).Value);

        Assert.Equal(_user.Id.ToString(), token.Subject);
        Assert.Equal("ana@citadent.ec", token.GetClaim("email").Value);
        Assert.Equal("Ana Torres", token.GetClaim("name").Value);
        Assert.Equal("Dentist", token.GetClaim("role").Value);
        Assert.Equal("CitaDent", token.Issuer);
        Assert.Equal("CitaDent.Api", Assert.Single(token.Audiences));
        Assert.False(string.IsNullOrEmpty(token.Id));
    }

    [Fact]
    public void Issue_ExpiresAfterTheConfiguredMinutes()
    {
        var accessToken = Issuer(Settings(minutes: 45)).Issue(_user);
        var token = new JsonWebToken(accessToken.Value);

        Assert.Equal(Now.AddMinutes(45), accessToken.ExpiresAt);
        Assert.Equal(Now.AddMinutes(45).UtcDateTime, token.ValidTo);
        Assert.Equal(Now.UtcDateTime, token.IssuedAt);
    }

    [Fact]
    public void Issue_GivesEachTokenItsOwnId()
    {
        var issuer = Issuer();

        var first = new JsonWebToken(issuer.Issue(_user).Value);
        var second = new JsonWebToken(issuer.Issue(_user).Value);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task TheToken_IsValid_WithTheConfiguredKey()
    {
        var token = Issuer().Issue(_user).Value;

        var result = await Validate(token, SigningKey);

        Assert.True(result.IsValid, result.Exception?.Message);
    }

    [Fact]
    public async Task TheToken_IsRejected_WithAnotherKey()
    {
        var token = Issuer().Issue(_user).Value;

        var result = await Validate(token, "another-signing-key-with-at-least-32-bytes");

        Assert.False(result.IsValid);
        Assert.IsAssignableFrom<SecurityTokenException>(result.Exception);
    }

    [Fact]
    public void TheValidator_AcceptsCompleteSettings()
    {
        var result = new JwtOptionsValidator().Validate(null, Settings());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short-key")]
    [InlineData("0123456789012345678901234567890")]
    public void TheValidator_RejectsAMissingOrShortKey(string key)
    {
        var result = new JwtOptionsValidator().Validate(null, Settings(key: key));

        Assert.True(result.Failed);
        Assert.Contains("Jwt:SigningKey", result.FailureMessage);
    }

    [Fact]
    public void TheValidator_RejectsMissingIssuerAudienceAndExpiration()
    {
        var settings = new JwtOptions { SigningKey = SigningKey, ExpirationMinutes = 0 };

        var result = new JwtOptionsValidator().Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains("Jwt:Issuer", result.FailureMessage);
        Assert.Contains("Jwt:Audience", result.FailureMessage);
        Assert.Contains("Jwt:ExpirationMinutes", result.FailureMessage);
    }

    // Lifetime is not checked here because the fake clock is not the real one; expiry is covered above.
    private static Task<TokenValidationResult> Validate(string token, string key) =>
        new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = "CitaDent",
            ValidAudience = "CitaDent.Api",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = false
        });
}
