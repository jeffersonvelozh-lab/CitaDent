using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Users;

namespace WebApiRest.Tests.Appointments;

/// <summary>
/// Integration tests of GET /api/appointments, the agenda. Each test creates its own dentist (Mondays 08:00-12:00)
/// through the API and books on Monday, December 2, 2030, so the API, which runs on the real clock, accepts it.
/// All data is fictitious.
/// </summary>
public class AgendaEndpointTests(CitaDentApiFactory factory) : IClassFixture<CitaDentApiFactory>
{
    private const string Url = "/api/appointments";
    private const string Monday = "from=2030-12-02&to=2030-12-02";

    private readonly HttpClient _setup = factory.CreateClientWithToken(UserRole.Admin);

    // Booking stores who did it, and that column references users, so this one is a real user.
    private readonly HttpClient _reception = factory.CreateClientForUser(factory.ActiveUser);

    private sealed record Scenario(Guid PatientId, Guid DentistId, Guid ChairId, Guid ProcedureId, string ProcedureName);

    private static string MondayAt(string time) => $"2030-12-02T{time}:00-05:00";

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private async Task<Guid> CreateAsync(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> NewDentistAsync(string firstName = "Carla")
    {
        var id = await CreateAsync(_setup, "/api/dentists", new
        {
            email = $"dentist-{Guid.NewGuid():N}@citadent.test",
            password = "Clave-Segura-123",
            firstName,
            lastName = "Mora",
            licenseNumber = $"LIC-{Guid.NewGuid():N}"[..20]
        });

        var schedule = await _setup.PutAsJsonAsync($"/api/dentists/{id}/schedule", new[]
        {
            new { day = "Monday", start = "08:00", end = "12:00" }
        });
        Assert.Equal(HttpStatusCode.OK, schedule.StatusCode);

        return id;
    }

    private async Task<Scenario> NewScenarioAsync(string dentistFirstName = "Carla")
    {
        var patientId = await CreateAsync(_reception, "/api/patients", new
        {
            documentType = "Passport",
            documentNumber = $"P{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            firstName = "Ana",
            lastName = "Pérez",
            birthDate = "1990-05-20"
        });
        var procedureName = $"Limpieza {Guid.NewGuid():N}"[..20];
        var procedureId = await CreateAsync(_setup, "/api/procedures", new
        {
            name = procedureName,
            durationMinutes = 30,
            basePrice = 25m
        });
        var chairId = await CreateAsync(_setup, "/api/chairs", new { name = $"Sillon {Guid.NewGuid():N}" });

        return new Scenario(patientId, await NewDentistAsync(dentistFirstName), chairId, procedureId, procedureName);
    }

    private Task<Guid> BookAsync(Scenario s, string time) =>
        CreateAsync(_reception, Url, new
        {
            patientId = s.PatientId,
            dentistId = s.DentistId,
            chairId = s.ChairId,
            procedureId = s.ProcedureId,
            startsAt = MondayAt(time)
        });

    private async Task<JsonElement> ListAsync(string query, HttpClient? client = null)
    {
        var response = await (client ?? _reception).GetAsync($"{Url}?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    private static List<Guid> Ids(JsonElement list) => list.EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).ToList();

    [Fact]
    public async Task List_ForADentist_Returns200WithTheNamesAndTheSlotInUtc()
    {
        var s = await NewScenarioAsync("Luis");
        var id = await BookAsync(s, "09:00");

        var item = Assert.Single((await ListAsync($"{Monday}&dentistId={s.DentistId}")).EnumerateArray());

        Assert.Equal(id, item.GetProperty("id").GetGuid());
        Assert.Equal("2030-12-02T14:00:00+00:00", item.GetProperty("startsAt").GetString());
        Assert.Equal("2030-12-02T14:30:00+00:00", item.GetProperty("endsAt").GetString());
        Assert.Equal("Scheduled", item.GetProperty("status").GetString());
        Assert.Equal(s.PatientId, item.GetProperty("patientId").GetGuid());
        Assert.Equal("Ana Pérez", item.GetProperty("patientName").GetString());
        Assert.Equal(s.DentistId, item.GetProperty("dentistId").GetGuid());
        Assert.Equal("Luis Mora", item.GetProperty("dentistName").GetString());
        Assert.Equal(s.ChairId, item.GetProperty("chairId").GetGuid());
        Assert.StartsWith("Sillon ", item.GetProperty("chairName").GetString());
        Assert.Equal(s.ProcedureId, item.GetProperty("procedureId").GetGuid());
        Assert.Equal(s.ProcedureName, item.GetProperty("procedureName").GetString());
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Dentist)]
    public async Task List_AsAnyRole_Returns200(UserRole role)
    {
        var s = await NewScenarioAsync();
        var id = await BookAsync(s, "10:00");

        var list = await ListAsync($"{Monday}&dentistId={s.DentistId}", factory.CreateClientWithToken(role));

        Assert.Equal([id], Ids(list));
    }

    [Fact]
    public async Task List_IncludesCancelledAppointments_OrderedByStart()
    {
        var s = await NewScenarioAsync();
        var later = await BookAsync(s, "11:00");
        var earlier = await BookAsync(s, "08:00");
        var cancel = await _reception.PostAsJsonAsync($"{Url}/{later}/cancel", new { reason = "El paciente viaja" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var list = await ListAsync($"{Monday}&dentistId={s.DentistId}");

        Assert.Equal([earlier, later], Ids(list));
        Assert.Equal("Cancelled", list[1].GetProperty("status").GetString());
        Assert.Equal("El paciente viaja", list[1].GetProperty("cancelReason").GetString());
    }

    [Fact]
    public async Task List_WithoutADentist_IncludesEveryDentist()
    {
        var (first, second) = (await NewScenarioAsync(), await NewScenarioAsync());
        var firstId = await BookAsync(first, "09:30");
        var secondId = await BookAsync(second, "09:30");

        var ids = Ids(await ListAsync(Monday));

        Assert.Contains(firstId, ids);
        Assert.Contains(secondId, ids);
    }

    [Fact]
    public async Task List_OfOtherDays_LeavesTheAppointmentOut()
    {
        var s = await NewScenarioAsync();
        await BookAsync(s, "08:30");

        var list = await ListAsync($"from=2030-12-03&to=2030-12-09&dentistId={s.DentistId}");

        Assert.Empty(list.EnumerateArray());
    }

    [Fact]
    public async Task List_ForAnUnknownDentist_ReturnsAnEmptyList()
    {
        var list = await ListAsync($"{Monday}&dentistId={Guid.CreateVersion7()}");

        Assert.Empty(list.EnumerateArray());
    }

    [Theory]
    [InlineData("to=2030-12-02")] // No "from".
    [InlineData("from=2030-12-02")] // No "to".
    [InlineData("from=02/12/2030&to=2030-12-02")] // Not yyyy-MM-dd.
    [InlineData("from=2030-12-09&to=2030-12-02")] // The last day before the first.
    [InlineData("from=2030-12-01&to=2031-01-01")] // 32 days.
    public async Task List_WithInvalidDays_Returns400Validation(string query)
    {
        var response = await _reception.GetAsync($"{Url}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync($"{Url}?{Monday}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
