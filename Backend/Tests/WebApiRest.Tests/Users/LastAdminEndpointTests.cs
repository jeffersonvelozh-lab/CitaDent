using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Users;

namespace WebApiRest.Tests.Users;

/// <summary>
/// The clinic always keeps an active admin. This class has its own API factory, so its own database, where
/// the only admin at the start is the one the API seeds; a single test builds the situation step by step.
/// </summary>
public class LastAdminEndpointTests(CitaDentApiFactory factory) : IClassFixture<CitaDentApiFactory>
{
    private const string Url = "/api/users";

    private readonly HttpClient _admin = factory.CreateClientWithToken(UserRole.Admin);

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task TheLastActiveAdmin_CannotBeDeactivatedOrDemoted_UntilAnotherAdminExists()
    {
        var admins = await ReadAsync(await _admin.GetAsync($"{Url}?includeInactive=true&role=Admin"));
        var seeded = Assert.Single(admins.EnumerateArray());
        Assert.Equal(CitaDentApiFactory.SeedAdminEmail, seeded.GetProperty("email").GetString());
        var seededId = seeded.GetProperty("id").GetGuid();

        // The only active admin.
        await AssertProblemAsync(
            await _admin.PostAsync($"{Url}/{seededId}/deactivate", null), HttpStatusCode.Conflict, "LAST-ADMIN");
        await AssertProblemAsync(
            await _admin.PutAsJsonAsync($"{Url}/{seededId}/role", new { role = "Reception" }),
            HttpStatusCode.Conflict, "LAST-ADMIN");

        var login = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { email = CitaDentApiFactory.SeedAdminEmail, password = CitaDentApiFactory.SeedAdminPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("Admin", (await ReadAsync(login)).GetProperty("role").GetString());

        // With a second active admin, the first one can be deactivated.
        var second = await _admin.PostAsJsonAsync(Url, new
        {
            email = "segunda.admin@citadent.test",
            fullName = "Segunda Administración",
            password = "Clave-Admin-Dos-2026",
            role = "Admin"
        });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondId = (await ReadAsync(second)).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await _admin.PostAsync($"{Url}/{seededId}/deactivate", null)).StatusCode);

        // Now the second one is the last active admin.
        await AssertProblemAsync(
            await _admin.PostAsync($"{Url}/{secondId}/deactivate", null), HttpStatusCode.Conflict, "LAST-ADMIN");
    }
}
