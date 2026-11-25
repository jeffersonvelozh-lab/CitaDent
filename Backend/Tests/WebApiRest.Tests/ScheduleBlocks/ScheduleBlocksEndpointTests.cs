using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain.Users;

namespace WebApiRest.Tests.ScheduleBlocks;

/// <summary>
/// Integration tests of /api/schedule-blocks: any role reads, reception and admin create and remove.
/// The API runs on the real clock, so "future" blocks are in 2030 and "past" ones in 2020. All data is fictitious.
/// </summary>
public class ScheduleBlocksEndpointTests(CitaDentApiFactory factory) : IClassFixture<CitaDentApiFactory>
{
    private const string Url = "/api/schedule-blocks";
    private const string StartsAt = "2030-12-24T08:00:00-05:00";
    private const string EndsAt = "2030-12-24T18:00:00-05:00";

    private readonly HttpClient _admin = factory.CreateClientWithToken(UserRole.Admin);
    private readonly HttpClient _reception = factory.CreateClientWithToken(UserRole.Reception);

    private static object Block(Guid? dentistId = null, string startsAt = StartsAt, string endsAt = EndsAt, string? reason = "Feriado") =>
        new { dentistId, startsAt, endsAt, reason };

    private async Task<Guid> CreateAsync(HttpClient? client = null, object? body = null)
    {
        var response = await (client ?? _reception).PostAsJsonAsync(Url, body ?? Block());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> RegisterDentistAsync()
    {
        var response = await _admin.PostAsJsonAsync("/api/dentists", new
        {
            email = $"dentist-{Guid.NewGuid():N}@citadent.test",
            password = "Clave-Segura-123",
            firstName = "Carla",
            lastName = "Mora",
            licenseNumber = $"LIC-{Guid.NewGuid():N}"[..20]
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> PostRawAsync(string json) =>
        _reception.PostAsync(Url, new StringContent(json, Encoding.UTF8, "application/json"));

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await ReadAsync(response)).GetProperty("code").GetString());
    }

    // Create

    [Theory]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Admin)]
    public async Task Create_ForTheWholeClinic_Returns201WithLocationAndTheRangeInUtc(UserRole role)
    {
        var response = await factory.CreateClientWithToken(role).PostAsJsonAsync(Url, Block(reason: "  Nochebuena "));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("dentistId").ValueKind);
        Assert.Equal("2030-12-24T13:00:00+00:00", body.GetProperty("startsAt").GetString());
        Assert.Equal("2030-12-24T23:00:00+00:00", body.GetProperty("endsAt").GetString());
        Assert.Equal("Nochebuena", body.GetProperty("reason").GetString());

        Assert.NotNull(response.Headers.Location);
        var fetched = await _admin.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(body.GetProperty("id").GetGuid(), (await ReadAsync(fetched)).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Create_ForADentist_KeepsTheDentist()
    {
        var dentistId = await RegisterDentistAsync();

        var response = await _reception.PostAsJsonAsync(Url, Block(dentistId, reason: "Vacaciones"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(dentistId, (await ReadAsync(response)).GetProperty("dentistId").GetGuid());
    }

    [Fact]
    public async Task Create_ForADentistThatDoesNotExist_Returns400Validation()
    {
        var response = await _reception.PostAsJsonAsync(Url, Block(Guid.CreateVersion7()));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Fact]
    public async Task Create_AsDentist_Returns403()
    {
        var response = await factory.CreateClientWithToken(UserRole.Dentist).PostAsJsonAsync(Url, Block());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(Url, Block());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("2030-12-24T18:00:00-05:00", "2030-12-24T08:00:00-05:00")] // Reversed.
    [InlineData("2030-12-24T08:00:00-05:00", "2030-12-24T13:00:00Z")] // The same instant, written two ways.
    [InlineData("2020-12-24T08:00:00-05:00", "2020-12-24T18:00:00-05:00")] // Already ended.
    public async Task Create_WithARangeThatIsReversedEmptyOrPast_Returns400Validation(string startsAt, string endsAt)
    {
        var response = await _reception.PostAsJsonAsync(Url, Block(startsAt: startsAt, endsAt: endsAt));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Theory]
    [InlineData("""{ "startsAt": "2030-12-24T08:00:00", "endsAt": "2030-12-24T18:00:00-05:00", "reason": "Feriado" }""")]
    [InlineData("""{ "startsAt": "2030-12-24T08:00:00-05:00", "endsAt": "2030-12-24", "reason": "Feriado" }""")]
    [InlineData("""{ "startsAt": "24/12/2030 08:00", "endsAt": "2030-12-24T18:00:00-05:00", "reason": "Feriado" }""")]
    [InlineData("""{ "startsAt": 1924347600, "endsAt": "2030-12-24T18:00:00-05:00", "reason": "Feriado" }""")]
    public async Task Create_WithAnInstantWithoutOffsetOrMalformed_Returns400Validation(string json)
    {
        var response = await PostRawAsync(json);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Theory]
    [InlineData("""{ "endsAt": "2030-12-24T18:00:00-05:00", "reason": "Feriado" }""")]
    [InlineData("""{ "startsAt": "2030-12-24T08:00:00-05:00", "endsAt": null, "reason": "Feriado" }""")]
    [InlineData("""{ "startsAt": "2030-12-24T08:00:00-05:00", "endsAt": "2030-12-24T18:00:00-05:00" }""")]
    [InlineData("""{ "startsAt": "2030-12-24T08:00:00-05:00", "endsAt": "2030-12-24T18:00:00-05:00", "reason": "   " }""")]
    [InlineData("""{ "dentistId": "00000000-0000-0000-0000-000000000000", "startsAt": "2030-12-24T08:00:00-05:00", "endsAt": "2030-12-24T18:00:00-05:00", "reason": "Feriado" }""")]
    public async Task Create_WithAMissingInstantBlankReasonOrEmptyDentist_Returns400Validation(string json)
    {
        var response = await PostRawAsync(json);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    // Get

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Dentist)]
    public async Task Get_WorksForEveryRole(UserRole role)
    {
        var id = await CreateAsync();

        var response = await factory.CreateClientWithToken(role).GetAsync($"{Url}/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(id, (await ReadAsync(response)).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Get_WithAnUnknownId_Returns404NotFound()
    {
        var response = await _reception.GetAsync($"{Url}/{Guid.CreateVersion7()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NOT-FOUND");
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync($"{Url}/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // List. Its blocks are in March 2031, away from the other tests of this class.

    private async Task<Guid> CreateInMarch2031Async(Guid? dentistId, string startsAt, string endsAt) =>
        await CreateAsync(body: Block(dentistId, $"2031-03-{startsAt}-05:00", $"2031-03-{endsAt}-05:00", "Bloqueo"));

    private async Task<List<Guid>> ListIdsAsync(string query, HttpClient? client = null)
    {
        var response = await (client ?? _reception).GetAsync($"{Url}?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync(response)).EnumerateArray().Select(b => b.GetProperty("id").GetGuid()).ToList();
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Dentist)]
    public async Task List_WorksForEveryRole(UserRole role)
    {
        var id = await CreateInMarch2031Async(null, "03T08:00:00", "03T12:00:00");

        var ids = await ListIdsAsync("from=2031-03-03&to=2031-03-03", factory.CreateClientWithToken(role));

        Assert.Contains(id, ids);
    }

    [Fact]
    public async Task List_ReadsTheDaysAtTheClinic_AndOrdersByStart()
    {
        // 23:00 on March 4 at the clinic is already March 5 in UTC, but it belongs to March 4.
        var lateAtNight = await CreateInMarch2031Async(null, "04T23:00:00", "04T23:30:00");
        var morning = await CreateInMarch2031Async(null, "04T08:00:00", "04T09:00:00");
        var nextDay = await CreateInMarch2031Async(null, "05T08:00:00", "05T09:00:00");

        var ids = await ListIdsAsync("from=2031-03-04&to=2031-03-04");

        Assert.Contains(lateAtNight, ids);
        Assert.Contains(morning, ids);
        Assert.True(ids.IndexOf(morning) < ids.IndexOf(lateAtNight));
        Assert.DoesNotContain(nextDay, ids);
    }

    [Fact]
    public async Task List_ForADentist_IncludesTheClinicBlocksUnlessAskedNotTo()
    {
        var dentistId = await RegisterDentistAsync();
        var own = await CreateInMarch2031Async(dentistId, "06T08:00:00", "06T12:00:00");
        var otherDentist = await CreateInMarch2031Async(await RegisterDentistAsync(), "06T08:00:00", "06T12:00:00");
        var clinic = await CreateInMarch2031Async(null, "06T00:00:00", "07T00:00:00");

        var withClinic = await ListIdsAsync($"from=2031-03-06&to=2031-03-06&dentistId={dentistId}");
        var withoutClinic = await ListIdsAsync($"from=2031-03-06&to=2031-03-06&dentistId={dentistId}&includeClinicWide=false");

        Assert.Equal([clinic, own], withClinic);
        Assert.DoesNotContain(otherDentist, withClinic);
        Assert.Equal([own], withoutClinic);
    }

    [Fact]
    public async Task List_ForADentistThatDoesNotExist_ReturnsOnlyTheClinicBlocks()
    {
        var clinic = await CreateInMarch2031Async(null, "07T08:00:00", "07T12:00:00");

        var withClinic = await ListIdsAsync($"from=2031-03-07&to=2031-03-07&dentistId={Guid.CreateVersion7()}");
        var withoutClinic = await ListIdsAsync(
            $"from=2031-03-07&to=2031-03-07&dentistId={Guid.CreateVersion7()}&includeClinicWide=false");

        Assert.Contains(clinic, withClinic);
        Assert.Empty(withoutClinic);
    }

    [Theory]
    [InlineData("to=2031-03-31")]
    [InlineData("from=2031-03-01")]
    [InlineData("from=01/03/2031&to=2031-03-31")] // Would be read as January 3.
    [InlineData("from=2031-3-1&to=2031-03-31")]
    [InlineData("from=2031-03-01T00:00:00&to=2031-03-31")]
    [InlineData("from=&to=2031-03-31")]
    [InlineData("from=2031-03-01&to=2031-02-28")]
    [InlineData("from=2031-01-01&to=2032-01-02")] // 367 days.
    [InlineData("from=2031-03-01&to=2031-03-31&dentistId=abc")]
    public async Task List_WithMissingMalformedReversedOrTooLongDays_Returns400Validation(string query)
    {
        var response = await _reception.GetAsync($"{Url}?{query}");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync($"{Url}?from=2031-03-01&to=2031-03-31");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Remove

    [Theory]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Admin)]
    public async Task Remove_Returns204AndTheBlockIsGone(UserRole role)
    {
        var id = await CreateAsync();

        var response = await factory.CreateClientWithToken(role).DeleteAsync($"{Url}/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertProblemAsync(await _reception.GetAsync($"{Url}/{id}"), HttpStatusCode.NotFound, "NOT-FOUND");
    }

    [Fact]
    public async Task Remove_WithAnUnknownId_Returns404NotFound()
    {
        var response = await _reception.DeleteAsync($"{Url}/{Guid.CreateVersion7()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NOT-FOUND");
    }

    [Fact]
    public async Task Remove_AsDentist_Returns403AndKeepsTheBlock()
    {
        var id = await CreateAsync();

        var response = await factory.CreateClientWithToken(UserRole.Dentist).DeleteAsync($"{Url}/{id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _reception.GetAsync($"{Url}/{id}")).StatusCode);
    }

    [Fact]
    public async Task Remove_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().DeleteAsync($"{Url}/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
