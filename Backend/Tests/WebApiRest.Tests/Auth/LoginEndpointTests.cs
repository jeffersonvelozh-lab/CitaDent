using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace WebApiRest.Tests.Auth;

/// <summary>Integration tests of POST /api/auth/login. They need the PostgreSQL from docker-compose.</summary>
public class LoginEndpointTests(CitaDentApiFactory factory) : IClassFixture<CitaDentApiFactory>
{
    private const string LoginUrl = "/api/auth/login";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenWithRoleAndExpiration()
    {
        var before = DateTimeOffset.UtcNow;

        var response = await _client.PostAsJsonAsync(
            LoginUrl, new { email = CitaDentApiFactory.ActiveEmail, password = CitaDentApiFactory.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        var accessToken = root.GetProperty("accessToken").GetString();
        var expiresAt = root.GetProperty("expiresAt").GetDateTimeOffset();

        Assert.False(string.IsNullOrEmpty(accessToken));
        Assert.Equal("Bearer", root.GetProperty("tokenType").GetString());
        Assert.Equal("Reception", root.GetProperty("role").GetString());

        // appsettings.json sets 60 minutes; the issuer truncates to whole seconds.
        Assert.InRange(expiresAt, before.AddMinutes(60).AddSeconds(-2), DateTimeOffset.UtcNow.AddMinutes(60));
    }

    [Fact]
    public async Task Login_WithValidCredentials_TokenIsSignedWithTheConfiguredKey()
    {
        var response = await _client.PostAsJsonAsync(
            LoginUrl, new { email = CitaDentApiFactory.ActiveEmail, password = CitaDentApiFactory.Password });
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var accessToken = body.RootElement.GetProperty("accessToken").GetString();

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken, new TokenValidationParameters
        {
            ValidIssuer = CitaDentApiFactory.Issuer,
            ValidAudience = CitaDentApiFactory.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CitaDentApiFactory.SigningKey))
        });

        Assert.True(validation.IsValid, validation.Exception?.Message);
        Assert.Equal(factory.ActiveUser.Id.ToString(), validation.Claims["sub"]);
        Assert.Equal("Reception", validation.Claims["role"]);
    }

    [Theory]
    [InlineData(CitaDentApiFactory.ActiveEmail, "clave-incorrecta")]
    [InlineData("nadie@citadent.test", CitaDentApiFactory.Password)]
    [InlineData(CitaDentApiFactory.InactiveEmail, CitaDentApiFactory.Password)]
    public async Task Login_WithInvalidCredentials_Returns401WithTheSameProblem(string email, string password)
    {
        var response = await _client.PostAsJsonAsync(LoginUrl, new { email, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("AUTH-INVALID-CREDENTIALS", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("The email or password is incorrect.", body.RootElement.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("", CitaDentApiFactory.Password)]
    [InlineData(CitaDentApiFactory.ActiveEmail, "")]
    [InlineData(null, null)]
    public async Task Login_WithMissingFields_Returns400Validation(string? email, string? password)
    {
        var response = await _client.PostAsJsonAsync(LoginUrl, new { email, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VALIDATION", body.RootElement.GetProperty("code").GetString());
    }
}
