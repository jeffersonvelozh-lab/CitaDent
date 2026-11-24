using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace WebApiRest.Tests.Auth;

/// <summary>The API creates the first admin at startup from configuration (Seed:Admin:*).</summary>
public class InitialAdminSeedingTests(CitaDentApiFactory factory) : IClassFixture<CitaDentApiFactory>
{
    [Fact]
    public async Task SeededAdmin_CanLogInAndReachAnAdminOnlyEndpoint()
    {
        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = CitaDentApiFactory.SeedAdminEmail,
            password = CitaDentApiFactory.SeedAdminPassword
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.Equal("Admin", body.RootElement.GetProperty("role").GetString());

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.RootElement.GetProperty("accessToken").GetString());
        var response = await client.GetAsync("/test/authorization/admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Startup_WithAShortSeedPassword_Fails()
    {
        using var misconfigured = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Seed:Admin:Password", "corta"));

        var ex = Assert.Throws<InvalidOperationException>(() => misconfigured.CreateClient());

        Assert.Contains("Seed:Admin", ex.Message);
    }
}
