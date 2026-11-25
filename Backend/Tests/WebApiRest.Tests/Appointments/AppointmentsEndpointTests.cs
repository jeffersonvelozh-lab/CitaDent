using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain.Users;

namespace WebApiRest.Tests.Appointments;

/// <summary>
/// Integration tests of /api/appointments: reception and admin book, any role reads. Each test creates its own
/// patient, dentist (Mondays 08:00-12:00), chair and 30-minute procedure through the API. The API runs on the
/// real clock, so appointments are on Monday, December 2, 2030. All data is fictitious.
/// </summary>
public class AppointmentsEndpointTests(CitaDentApiFactory factory) : IClassFixture<CitaDentApiFactory>
{
    private const string Url = "/api/appointments";

    // Setup needs no real user: creating patients, dentists, chairs and procedures stores no author.
    private readonly HttpClient _setup = factory.CreateClientWithToken(UserRole.Admin);

    // Booking stores who did it, and that column references users, so this one is a real user.
    private readonly HttpClient _reception = factory.CreateClientForUser(factory.ActiveUser);

    private sealed record Scenario(Guid PatientId, Guid DentistId, Guid ChairId, Guid ProcedureId);

    private static string MondayAt(string time, int day = 2) => $"2030-12-{day:00}T{time}:00-05:00";

    // A block of the whole clinic affects every test of this class, which share the database, so the block test
    // uses a Monday no other test books.
    private const int BlockedMonday = 9;

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await ReadAsync(response)).GetProperty("code").GetString());
    }

    private async Task<Guid> CreateAsync(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> NewDentistAsync()
    {
        var id = await CreateAsync(_setup, "/api/dentists", new
        {
            email = $"dentist-{Guid.NewGuid():N}@citadent.test",
            password = "Clave-Segura-123",
            firstName = "Carla",
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

    private Task<Guid> NewChairAsync() => CreateAsync(_setup, "/api/chairs", new { name = $"Sillon {Guid.NewGuid():N}" });

    private async Task<Scenario> NewScenarioAsync()
    {
        var patientId = await CreateAsync(_reception, "/api/patients", new
        {
            documentType = "Passport",
            documentNumber = $"P{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            firstName = "Ana",
            lastName = "Pérez",
            birthDate = "1990-05-20"
        });
        var procedureId = await CreateAsync(_setup, "/api/procedures", new
        {
            name = $"Limpieza {Guid.NewGuid():N}",
            durationMinutes = 30,
            basePrice = 25m
        });

        return new Scenario(patientId, await NewDentistAsync(), await NewChairAsync(), procedureId);
    }

    private static object Booking(Scenario s, string startsAt, Guid? dentistId = null, Guid? chairId = null) => new
    {
        patientId = s.PatientId,
        dentistId = dentistId ?? s.DentistId,
        chairId = chairId ?? s.ChairId,
        procedureId = s.ProcedureId,
        startsAt
    };

    private Task<HttpResponseMessage> BookAsync(object body, HttpClient? client = null) =>
        (client ?? _reception).PostAsJsonAsync(Url, body);

    private async Task<Guid> BookOkAsync(object body)
    {
        var response = await BookAsync(body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    // Booking

    [Fact]
    public async Task Schedule_AsReception_Returns201WithTheSlotOfTheProcedureAndTheUserFromTheToken()
    {
        var s = await NewScenarioAsync();

        var response = await BookAsync(Booking(s, MondayAt("09:00")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(s.PatientId, body.GetProperty("patientId").GetGuid());
        Assert.Equal(s.DentistId, body.GetProperty("dentistId").GetGuid());
        Assert.Equal(s.ChairId, body.GetProperty("chairId").GetGuid());
        Assert.Equal(s.ProcedureId, body.GetProperty("procedureId").GetGuid());
        Assert.Equal("2030-12-02T14:00:00+00:00", body.GetProperty("startsAt").GetString());
        Assert.Equal("2030-12-02T14:30:00+00:00", body.GetProperty("endsAt").GetString()); // RN-01
        Assert.Equal("Scheduled", body.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("cancelReason").ValueKind);
        Assert.Equal(factory.ActiveUser.Id, body.GetProperty("createdByUserId").GetGuid());

        Assert.NotNull(response.Headers.Location);
        var fetched = await _reception.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(body.GetProperty("id").GetGuid(), (await ReadAsync(fetched)).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Schedule_AsAdmin_KeepsTheAdminAsTheOneWhoBookedIt()
    {
        var s = await NewScenarioAsync();
        var login = await ReadAsync(await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new
        {
            email = CitaDentApiFactory.SeedAdminEmail,
            password = CitaDentApiFactory.SeedAdminPassword
        }));
        var admin = factory.CreateClientWithToken(login.GetProperty("accessToken").GetString()!);
        var users = await ReadAsync(await admin.GetAsync("/api/users?role=Admin"));
        var adminId = users.EnumerateArray()
            .Single(u => u.GetProperty("email").GetString() == CitaDentApiFactory.SeedAdminEmail)
            .GetProperty("id").GetGuid();

        var response = await BookAsync(Booking(s, MondayAt("09:00")), admin);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(adminId, (await ReadAsync(response)).GetProperty("createdByUserId").GetGuid());
    }

    [Fact]
    public async Task Schedule_RightAfterAnotherAppointment_Returns201_RN04()
    {
        var s = await NewScenarioAsync();
        await BookOkAsync(Booking(s, MondayAt("09:00")));

        var response = await BookAsync(Booking(s, MondayAt("09:30")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Schedule_AsDentist_Returns403()
    {
        var response = await BookAsync(new { }, factory.CreateClientWithToken(UserRole.Dentist));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Schedule_WithoutToken_Returns401()
    {
        var response = await BookAsync(new { }, factory.CreateClient());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Malformed requests

    [Theory]
    [InlineData("patientId")]
    [InlineData("dentistId")]
    [InlineData("chairId")]
    [InlineData("procedureId")]
    [InlineData("startsAt")]
    public async Task Schedule_WithAMissingField_Returns400Validation(string missing)
    {
        var fields = new Dictionary<string, object>
        {
            ["patientId"] = Guid.CreateVersion7(),
            ["dentistId"] = Guid.CreateVersion7(),
            ["chairId"] = Guid.CreateVersion7(),
            ["procedureId"] = Guid.CreateVersion7(),
            ["startsAt"] = MondayAt("09:00")
        };
        fields.Remove(missing);

        var response = await BookAsync(fields);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Fact]
    public async Task Schedule_WithAStartWithoutOffset_Returns400Validation()
    {
        var s = await NewScenarioAsync();

        var response = await BookAsync(Booking(s, "2030-12-02T09:00:00"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Theory]
    [InlineData("patient")]
    [InlineData("dentist")]
    [InlineData("chair")]
    [InlineData("procedure")]
    public async Task Schedule_WithAReferenceThatDoesNotExist_Returns400Validation(string what)
    {
        var s = await NewScenarioAsync();
        var unknown = Guid.CreateVersion7();
        var broken = what switch
        {
            "patient" => s with { PatientId = unknown },
            "dentist" => s with { DentistId = unknown },
            "chair" => s with { ChairId = unknown },
            _ => s with { ProcedureId = unknown }
        };

        var response = await BookAsync(Booking(broken, MondayAt("09:00")));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Fact]
    public async Task Schedule_WithEmptyGuids_Returns400Validation()
    {
        var response = await _reception.PostAsync(Url, Json($$"""
            {
              "patientId": "{{Guid.Empty}}",
              "dentistId": "{{Guid.Empty}}",
              "chairId": "{{Guid.Empty}}",
              "procedureId": "{{Guid.Empty}}",
              "startsAt": "{{MondayAt("09:00")}}"
            }
            """));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    // Business rules

    [Fact]
    public async Task Schedule_InThePast_Returns422_RN06()
    {
        var s = await NewScenarioAsync();

        // A Monday long gone: the API runs on the real clock.
        var response = await BookAsync(Booking(s, "2021-12-06T09:00:00-05:00"));

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "RN-06");
    }

    [Fact]
    public async Task Schedule_WhenTheDentistIsBusy_Returns409_RN02()
    {
        var s = await NewScenarioAsync();
        await BookOkAsync(Booking(s, MondayAt("09:00")));

        var response = await BookAsync(Booking(s, MondayAt("09:15"), chairId: await NewChairAsync()));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "RN-02");
    }

    [Fact]
    public async Task Schedule_WhenTheChairIsBusy_Returns409_RN03()
    {
        var s = await NewScenarioAsync();
        await BookOkAsync(Booking(s, MondayAt("09:00")));

        var response = await BookAsync(Booking(s, MondayAt("09:15"), dentistId: await NewDentistAsync()));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "RN-03");
    }

    [Fact]
    public async Task Schedule_OutsideTheWorkingHours_Returns409_RN05()
    {
        var s = await NewScenarioAsync();

        // Ends at 12:15, after the dentist's day.
        var response = await BookAsync(Booking(s, MondayAt("11:45")));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "RN-05");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Schedule_InABlockCreatedThroughTheApi_Returns409_RN05(bool wholeClinic)
    {
        var s = await NewScenarioAsync();
        await CreateAsync(_reception, "/api/schedule-blocks", new
        {
            dentistId = wholeClinic ? (Guid?)null : s.DentistId,
            startsAt = MondayAt("08:00", BlockedMonday),
            endsAt = MondayAt("10:00", BlockedMonday),
            reason = wholeClinic ? "Feriado" : "Vacaciones"
        });

        var response = await BookAsync(Booking(s, MondayAt("09:00", BlockedMonday)));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "RN-05");
    }

    // Reading

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Dentist)]
    public async Task Get_WorksForEveryRole(UserRole role)
    {
        var id = await BookOkAsync(Booking(await NewScenarioAsync(), MondayAt("10:00")));

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
}
