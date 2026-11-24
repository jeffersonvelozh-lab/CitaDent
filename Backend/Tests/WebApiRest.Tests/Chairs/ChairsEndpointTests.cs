using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain.Users;

namespace WebApiRest.Tests.Chairs;

/// <summary>Integration tests of /api/chairs: any role reads, only Admin writes.</summary>
public class ChairsEndpointTests(CitaDentApiFactory factory) : IClassFixture<CitaDentApiFactory>
{
    private const string Url = "/api/chairs";

    private readonly HttpClient _admin = factory.CreateClientWithToken(UserRole.Admin);

    // Names are unique, and the database is shared by the tests of this class. At most 50 characters.
    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private async Task<Guid> CreateAsync(string name)
    {
        var response = await _admin.PostAsJsonAsync(Url, new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await ReadAsync(response)).GetProperty("code").GetString());
    }

    private async Task<List<Guid>> ListIdsAsync(bool includeInactive)
    {
        var response = await _admin.GetAsync($"{Url}?includeInactive={includeInactive.ToString().ToLowerInvariant()}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync(response)).EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ToList();
    }

    // Create

    [Fact]
    public async Task Create_AsAdmin_Returns201WithLocationAndBody()
    {
        var name = UniqueName("Sillon");

        var response = await _admin.PostAsJsonAsync(Url, new { name = $"  {name} " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal(name, body.GetProperty("name").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());

        Assert.NotNull(response.Headers.Location);
        var fetched = await _admin.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(id, (await ReadAsync(fetched)).GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Dentist)]
    public async Task Create_WithoutAdminRole_Returns403(UserRole role)
    {
        var response = await factory.CreateClientWithToken(role).PostAsJsonAsync(Url, new { name = UniqueName("Sillon") });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(Url, new { name = UniqueName("Sillon") });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithANameAlreadyUsedInOtherCase_Returns409Duplicate()
    {
        var name = UniqueName("Sillon");
        await CreateAsync(name);

        var response = await _admin.PostAsJsonAsync(Url, new { name = name.ToUpperInvariant() });

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "DUPLICATE");
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "name": null }""")]
    [InlineData("""{ "name": "   " }""")]
    [InlineData("""{ "name": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" }""")] // 51 characters
    public async Task Create_WithAMissingBlankOrTooLongName_Returns400Validation(string json)
    {
        var response = await _admin.PostAsync(Url, new StringContent(json, Encoding.UTF8, "application/json"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    // List and get

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Dentist)]
    public async Task ListAndGet_WorkForEveryRole(UserRole role)
    {
        var id = await CreateAsync(UniqueName("Sillon"));
        var client = factory.CreateClientWithToken(role);

        var list = await client.GetAsync(Url);
        var one = await client.GetAsync($"{Url}/{id}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains((await ReadAsync(list)).EnumerateArray(), c => c.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        Assert.Equal(id, (await ReadAsync(one)).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task List_IsOrderedByName()
    {
        var second = await CreateAsync(UniqueName("zz-b"));
        var first = await CreateAsync(UniqueName("zz-a"));

        var ids = await ListIdsAsync(includeInactive: false);

        Assert.True(ids.IndexOf(first) < ids.IndexOf(second));
    }

    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync(Url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404NotFound()
    {
        var response = await _admin.GetAsync($"{Url}/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NOT-FOUND");
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync($"{Url}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Update (rename)

    [Fact]
    public async Task Update_AsAdmin_Returns200AndTheNewNameIsStored()
    {
        var id = await CreateAsync(UniqueName("Sillon"));
        var newName = UniqueName("Renamed");

        var response = await _admin.PutAsJsonAsync($"{Url}/{id}", new { name = newName });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(newName, (await ReadAsync(response)).GetProperty("name").GetString());
        var fetched = await ReadAsync(await _admin.GetAsync($"{Url}/{id}"));
        Assert.Equal(newName, fetched.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Update_ToItsOwnNameInOtherCase_Returns200()
    {
        var name = UniqueName("Sillon");
        var id = await CreateAsync(name);

        var response = await _admin.PutAsJsonAsync($"{Url}/{id}", new { name = name.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(name.ToUpperInvariant(), (await ReadAsync(response)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Update_WithAnotherChairsName_Returns409Duplicate()
    {
        var taken = UniqueName("Sillon");
        await CreateAsync(taken);
        var id = await CreateAsync(UniqueName("Sillon"));

        var response = await _admin.PutAsJsonAsync($"{Url}/{id}", new { name = taken });

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "DUPLICATE");
    }

    [Fact]
    public async Task Update_WithoutName_Returns400Validation()
    {
        var id = await CreateAsync(UniqueName("Sillon"));

        var response = await _admin.PutAsJsonAsync($"{Url}/{id}", new { name = "  " });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Fact]
    public async Task Update_UnknownId_Returns404NotFound()
    {
        var response = await _admin.PutAsJsonAsync($"{Url}/{Guid.NewGuid()}", new { name = UniqueName("Sillon") });

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NOT-FOUND");
    }

    [Fact]
    public async Task Update_AsReception_Returns403()
    {
        var id = await CreateAsync(UniqueName("Sillon"));

        var response = await factory.CreateClientWithToken(UserRole.Reception)
            .PutAsJsonAsync($"{Url}/{id}", new { name = UniqueName("Sillon") });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Activate and deactivate

    [Fact]
    public async Task DeactivateAndActivate_Return204AndChangeWhatTheListShows()
    {
        var id = await CreateAsync(UniqueName("Sillon"));

        var deactivate = await _admin.PostAsync($"{Url}/{id}/deactivate", content: null);
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        Assert.DoesNotContain(await ListIdsAsync(includeInactive: false), x => x == id);
        Assert.Contains(await ListIdsAsync(includeInactive: true), x => x == id);
        Assert.False((await ReadAsync(await _admin.GetAsync($"{Url}/{id}"))).GetProperty("isActive").GetBoolean());

        var activate = await _admin.PostAsync($"{Url}/{id}/activate", content: null);
        Assert.Equal(HttpStatusCode.NoContent, activate.StatusCode);
        Assert.Contains(await ListIdsAsync(includeInactive: false), x => x == id);
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task SetActive_UnknownId_Returns404NotFound(string action)
    {
        var response = await _admin.PostAsync($"{Url}/{Guid.NewGuid()}/{action}", content: null);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NOT-FOUND");
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task SetActive_AsDentist_Returns403(string action)
    {
        var id = await CreateAsync(UniqueName("Sillon"));

        var response = await factory.CreateClientWithToken(UserRole.Dentist)
            .PostAsync($"{Url}/{id}/{action}", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task SetActive_WithoutToken_Returns401(string action)
    {
        var response = await factory.CreateClient().PostAsync($"{Url}/{Guid.NewGuid()}/{action}", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_IsNotOffered()
    {
        // Chairs are deactivated, never deleted, so the API has no DELETE.
        var id = await CreateAsync(UniqueName("Sillon"));

        var response = await _admin.DeleteAsync($"{Url}/{id}");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _admin.GetAsync($"{Url}/{id}")).StatusCode);
    }
}
