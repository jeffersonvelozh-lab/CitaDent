using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Users;

namespace WebApiRest.Tests.Procedures;

/// <summary>Integration tests of /api/procedures: any role reads, only Admin writes.</summary>
public class ProceduresEndpointTests(CitaDentApiFactory factory) : IClassFixture<CitaDentApiFactory>
{
    private const string Url = "/api/procedures";

    private readonly HttpClient _admin = factory.CreateClientWithToken(UserRole.Admin);

    // Names are unique, and the database is shared by the tests of this class.
    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static object Body(string name, int minutes = 30, decimal price = 25m, string? description = null) =>
        new { name, description, durationMinutes = minutes, basePrice = price };

    private async Task<JsonElement> CreateAsync(string name, int minutes = 30, decimal price = 25m)
    {
        var response = await _admin.PostAsJsonAsync(Url, Body(name, minutes, price));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync(response);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await ReadAsync(response)).GetProperty("code").GetString());
    }

    // Create

    [Fact]
    public async Task Create_AsAdmin_Returns201WithLocationAndBody()
    {
        var name = UniqueName("Endodoncia");

        var response = await _admin.PostAsJsonAsync(Url, Body(name, 90, 180m, "Tratamiento de conducto"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal(name, body.GetProperty("name").GetString());
        Assert.Equal("Tratamiento de conducto", body.GetProperty("description").GetString());
        Assert.Equal(90, body.GetProperty("durationMinutes").GetInt32());
        Assert.Equal(180m, body.GetProperty("basePrice").GetDecimal());
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
        var response = await factory.CreateClientWithToken(role).PostAsJsonAsync(Url, Body(UniqueName("Resina")));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(Url, Body(UniqueName("Resina")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithANameAlreadyUsedInOtherCase_Returns409Duplicate()
    {
        var name = UniqueName("Corona");
        await CreateAsync(name);

        var response = await _admin.PostAsJsonAsync(Url, Body(name.ToUpperInvariant()));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "DUPLICATE");
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(30, -5)]
    public async Task Create_BreakingRn16_Returns422(int minutes, decimal price)
    {
        var response = await _admin.PostAsJsonAsync(Url, Body(UniqueName("Sellante"), minutes, price));

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "RN-16");
    }

    [Fact]
    public async Task Create_WithoutName_Returns400Validation()
    {
        var response = await _admin.PostAsJsonAsync(Url, new { durationMinutes = 30, basePrice = 25m });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    // Read

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Dentist)]
    public async Task ListAndGet_WorkForEveryRole(UserRole role)
    {
        var created = await CreateAsync(UniqueName("Limpieza"));
        var id = created.GetProperty("id").GetGuid();
        var client = factory.CreateClientWithToken(role);

        var list = await client.GetAsync(Url);
        var one = await client.GetAsync($"{Url}/{id}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains((await ReadAsync(list)).EnumerateArray(), p => p.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
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

    // Update

    [Fact]
    public async Task Update_AsAdmin_Returns200WithTheNewData()
    {
        var id = (await CreateAsync(UniqueName("Profilaxis"))).GetProperty("id").GetGuid();
        var newName = UniqueName("Profilaxis completa");

        var response = await _admin.PutAsJsonAsync($"{Url}/{id}", Body(newName, 45, 35.5m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(newName, body.GetProperty("name").GetString());
        Assert.Equal(45, body.GetProperty("durationMinutes").GetInt32());
        Assert.Equal(35.5m, body.GetProperty("basePrice").GetDecimal());
    }

    [Fact]
    public async Task Update_WithAnotherProceduresName_Returns409Duplicate()
    {
        var takenName = UniqueName("Carilla");
        await CreateAsync(takenName);
        var id = (await CreateAsync(UniqueName("Incrustación"))).GetProperty("id").GetGuid();

        var response = await _admin.PutAsJsonAsync($"{Url}/{id}", Body(takenName));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "DUPLICATE");
    }

    [Fact]
    public async Task Update_UnknownId_Returns404()
    {
        var response = await _admin.PutAsJsonAsync($"{Url}/{Guid.NewGuid()}", Body(UniqueName("Puente")));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NOT-FOUND");
    }

    [Fact]
    public async Task Update_AsReception_Returns403()
    {
        var id = (await CreateAsync(UniqueName("Implante"))).GetProperty("id").GetGuid();

        var response = await factory.CreateClientWithToken(UserRole.Reception)
            .PutAsJsonAsync($"{Url}/{id}", Body(UniqueName("Implante")));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Activate / deactivate

    [Fact]
    public async Task DeactivateAndActivate_ChangeWhatTheListShows()
    {
        var id = (await CreateAsync(UniqueName("Blanqueamiento"))).GetProperty("id").GetGuid();

        var deactivate = await _admin.PostAsync($"{Url}/{id}/deactivate", content: null);
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        Assert.DoesNotContain(await ListIdsAsync(includeInactive: false), x => x == id);
        Assert.Contains(await ListIdsAsync(includeInactive: true), x => x == id);

        var activate = await _admin.PostAsync($"{Url}/{id}/activate", content: null);
        Assert.Equal(HttpStatusCode.NoContent, activate.StatusCode);
        Assert.Contains(await ListIdsAsync(includeInactive: false), x => x == id);
    }

    [Fact]
    public async Task Deactivate_AsDentist_Returns403()
    {
        var id = (await CreateAsync(UniqueName("Ortodoncia"))).GetProperty("id").GetGuid();

        var response = await factory.CreateClientWithToken(UserRole.Dentist)
            .PostAsync($"{Url}/{id}/deactivate", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_UnknownId_Returns404()
    {
        var response = await _admin.PostAsync($"{Url}/{Guid.NewGuid()}/deactivate", content: null);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NOT-FOUND");
    }

    private async Task<List<Guid>> ListIdsAsync(bool includeInactive)
    {
        var response = await _admin.GetAsync($"{Url}?includeInactive={includeInactive.ToString().ToLowerInvariant()}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync(response)).EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToList();
    }
}
