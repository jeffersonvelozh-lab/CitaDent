using System.Net;
using Domain.Users;

namespace WebApiRest.Tests.Auth;

/// <summary>Integration tests of token validation and the role policies, through test-only endpoints.</summary>
public class AuthorizationTests(CitaDentApiFactory factory) : IClassFixture<CitaDentApiFactory>
{
    private const string AdminUrl = "/test/authorization/admin";

    [Fact]
    public async Task Request_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync(AdminUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EndpointWithoutPolicy_WithoutToken_Returns401BecauseAuthenticationIsTheDefault()
    {
        var response = await factory.CreateClient().GetAsync("/test/authorization/authenticated");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Dentist)]
    public async Task EndpointWithoutPolicy_WithAnyRole_Returns200(UserRole role)
    {
        var response = await factory.CreateClientWithToken(role).GetAsync("/test/authorization/authenticated");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnly_WithReceptionToken_Returns403()
    {
        var response = await factory.CreateClientWithToken(UserRole.Reception).GetAsync(AdminUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnly_WithAdminToken_Returns200()
    {
        var response = await factory.CreateClientWithToken(UserRole.Admin).GetAsync(AdminUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("admin", UserRole.Dentist, HttpStatusCode.Forbidden)]
    [InlineData("agenda", UserRole.Admin, HttpStatusCode.OK)]
    [InlineData("agenda", UserRole.Reception, HttpStatusCode.OK)]
    [InlineData("agenda", UserRole.Dentist, HttpStatusCode.Forbidden)]
    [InlineData("clinical", UserRole.Admin, HttpStatusCode.OK)]
    [InlineData("clinical", UserRole.Dentist, HttpStatusCode.OK)]
    [InlineData("clinical", UserRole.Reception, HttpStatusCode.Forbidden)]
    public async Task Policies_AllowOnlyTheirRoles(string endpoint, UserRole role, HttpStatusCode expected)
    {
        var response = await factory.CreateClientWithToken(role).GetAsync($"/test/authorization/{endpoint}");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Token_SignedWithAnotherKey_Returns401()
    {
        var token = CitaDentApiFactory.IssueToken(
            UserRole.Admin, signingKey: "another-signing-key-with-at-least-32-bytes!!");

        var response = await factory.CreateClientWithToken(token).GetAsync(AdminUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_FromAnotherIssuer_Returns401()
    {
        var token = CitaDentApiFactory.IssueToken(UserRole.Admin, issuer: "SomeoneElse");

        var response = await factory.CreateClientWithToken(token).GetAsync(AdminUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_ExpiredBeyondTheClockSkew_Returns401()
    {
        // Issued 61 minutes ago with a 60-minute life: expired a minute ago, more than the 30-second skew.
        var clock = new FixedClock(DateTimeOffset.UtcNow.AddMinutes(-61));
        var token = CitaDentApiFactory.IssueToken(UserRole.Admin, clock: clock);

        var response = await factory.CreateClientWithToken(token).GetAsync(AdminUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_WithoutToken_IsNotBlocked()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
