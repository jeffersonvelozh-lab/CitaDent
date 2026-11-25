using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain.Users;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace WebApiRest.Tests.Dentists;

/// <summary>Integration tests of /api/dentists: any role reads, only Admin writes. All data is fictitious.</summary>
public class DentistsEndpointTests(CitaDentApiFactory factory) : IClassFixture<CitaDentApiFactory>
{
    private const string Url = "/api/dentists";
    private const string Password = "Clave-Odonto-2026";

    private readonly HttpClient _admin = factory.CreateClientWithToken(UserRole.Admin);

    // Emails and licenses are unique, and the database is shared by the tests of this class.
    private static string NewEmail() => $"dentist-{Guid.NewGuid():N}@citadent.test";

    private static string NewLicense() => $"LIC-{Guid.NewGuid():N}"[..20];

    private static object Registration(string? email = null, string? license = null, string password = Password) => new
    {
        email = email ?? NewEmail(),
        password,
        firstName = "Carla",
        lastName = "Mora",
        licenseNumber = license ?? NewLicense(),
        specialty = "Ortodoncia"
    };

    private static object Block(string day, string start, string end) => new { day, start, end };

    private async Task<Guid> RegisterAsync(object? body = null)
    {
        var response = await _admin.PostAsJsonAsync(Url, body ?? Registration());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> PutScheduleAsync(Guid id, params object[] blocks) =>
        _admin.PutAsJsonAsync($"{Url}/{id}/schedule", blocks);

    private Task<HttpResponseMessage> PutRawScheduleAsync(Guid id, string json) =>
        _admin.PutAsync($"{Url}/{id}/schedule", new StringContent(json, Encoding.UTF8, "application/json"));

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await ReadAsync(response)).GetProperty("code").GetString());
    }

    private static List<(string Day, string Start, string End)> Schedule(JsonElement dentist) =>
        dentist.GetProperty("weeklySchedule").EnumerateArray()
            .Select(h => (h.GetProperty("day").GetString()!, h.GetProperty("start").GetString()!,
                h.GetProperty("end").GetString()!))
            .ToList();

    // Register

    [Fact]
    public async Task Register_AsAdmin_Returns201WithLocationAndNoPassword()
    {
        var license = NewLicense();

        var response = await _admin.PostAsJsonAsync(Url, Registration(license: license));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var body = await ReadAsync(response);
        Assert.Equal("Carla", body.GetProperty("firstName").GetString());
        Assert.Equal("Mora", body.GetProperty("lastName").GetString());
        Assert.Equal("Ortodoncia", body.GetProperty("specialty").GetString());
        Assert.Equal(license, body.GetProperty("licenseNumber").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());
        Assert.NotEqual(Guid.Empty, body.GetProperty("userId").GetGuid());
        Assert.Empty(Schedule(body));
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, json, StringComparison.Ordinal);

        Assert.NotNull(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, (await _admin.GetAsync(response.Headers.Location)).StatusCode);
    }

    [Fact]
    public async Task Register_TheDentistCanSignInWithTheDentistRole()
    {
        var email = NewEmail();
        var created = await ReadAsync(await _admin.PostAsJsonAsync(Url, Registration(email)));

        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await ReadAsync(login);
        Assert.Equal("Dentist", body.GetProperty("role").GetString());
        var token = new JsonWebTokenHandler().ReadJsonWebToken(body.GetProperty("accessToken").GetString());
        Assert.Equal(created.GetProperty("userId").GetGuid().ToString(), token.Subject);
    }

    [Fact]
    public async Task Register_WithAnEmailAlreadyUsed_Returns409Duplicate()
    {
        var email = NewEmail();
        await RegisterAsync(Registration(email));

        var response = await _admin.PostAsJsonAsync(Url, Registration(email.ToUpperInvariant()));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "DUPLICATE");
    }

    [Fact]
    public async Task Register_WithTheEmailOfAnotherUser_Returns409Duplicate()
    {
        // The reception user created by the factory already has this email.
        var response = await _admin.PostAsJsonAsync(Url, Registration(CitaDentApiFactory.ActiveEmail));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "DUPLICATE");
    }

    [Fact]
    public async Task Register_WithALicenseAlreadyUsedInOtherCase_Returns409Duplicate()
    {
        var license = NewLicense();
        await RegisterAsync(Registration(license: license));

        var response = await _admin.PostAsJsonAsync(Url, Registration(license: license.ToLowerInvariant()));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "DUPLICATE");
    }

    [Fact]
    public async Task Register_ThatFails_LeavesNoUserBehind()
    {
        // The license clashes, so the new email must not end up with a user account either.
        var license = NewLicense();
        await RegisterAsync(Registration(license: license));
        var email = NewEmail();

        await _admin.PostAsJsonAsync(Url, Registration(email, license));

        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Theory]
    [InlineData("""{ "email": "carla@citadent.test", "password": "corta", "firstName": "Carla", "lastName": "Mora", "licenseNumber": "LIC-1" }""")]
    [InlineData("""{ "email": "no-es-email", "password": "Clave-Odonto-2026", "firstName": "Carla", "lastName": "Mora", "licenseNumber": "LIC-1" }""")]
    [InlineData("""{ "email": "carla@citadent.test", "password": "Clave-Odonto-2026", "firstName": "Carla", "lastName": "Mora" }""")]
    [InlineData("""{ "email": "carla@citadent.test", "password": "Clave-Odonto-2026", "lastName": "Mora", "licenseNumber": "LIC-1" }""")]
    [InlineData("""{ "password": "Clave-Odonto-2026", "firstName": "Carla", "lastName": "Mora", "licenseNumber": "LIC-1" }""")]
    public async Task Register_WithInvalidData_Returns400Validation(string json)
    {
        var response = await _admin.PostAsync(Url, new StringContent(json, Encoding.UTF8, "application/json"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Theory]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Dentist)]
    public async Task Register_WithoutAdminRole_Returns403(UserRole role)
    {
        var response = await factory.CreateClientWithToken(role).PostAsJsonAsync(Url, Registration());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(Url, Registration());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // List and get

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Dentist)]
    public async Task ListAndGet_WorkForEveryRole_WithTheSchedule(UserRole role)
    {
        var id = await RegisterAsync();
        Assert.Equal(HttpStatusCode.OK, (await PutScheduleAsync(id, Block("Monday", "08:00", "12:00"))).StatusCode);
        var client = factory.CreateClientWithToken(role);

        var list = await client.GetAsync(Url);
        var one = await client.GetAsync($"{Url}/{id}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listed = (await ReadAsync(list)).EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == id);
        Assert.Equal([("Monday", "08:00:00", "12:00:00")], Schedule(listed));
        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        Assert.Equal([("Monday", "08:00:00", "12:00:00")], Schedule(await ReadAsync(one)));
    }

    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync(Url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_WithInactiveFlag_Returns200()
    {
        // No endpoint deactivates dentists yet, so this only checks that the flag is accepted.
        var id = await RegisterAsync();

        var response = await _admin.GetAsync($"{Url}?includeInactive=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains((await ReadAsync(response)).EnumerateArray(), d => d.GetProperty("id").GetGuid() == id);
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

    // Update

    private static object Edit(string? license, string firstName = "Luisa", string lastName = "Vera",
        string? specialty = "Endodoncia") =>
        new { firstName, lastName, licenseNumber = license, specialty };

    private async Task<string> UserFullNameAsync(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CitaDentDbContext>();
        var user = await context.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        return user.FullName;
    }

    [Fact]
    public async Task Update_AsAdmin_Returns200AndChangesTheDentistAndTheUserFullName()
    {
        var id = await RegisterAsync();
        Assert.Equal(HttpStatusCode.OK, (await PutScheduleAsync(id, Block("Monday", "08:00", "12:00"))).StatusCode);
        var license = NewLicense();

        var response = await _admin.PutAsJsonAsync($"{Url}/{id}", Edit($" {license} ", " Luisa ", " Vera "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Luisa", body.GetProperty("firstName").GetString());
        Assert.Equal("Vera", body.GetProperty("lastName").GetString());
        Assert.Equal(license, body.GetProperty("licenseNumber").GetString());
        Assert.Equal("Endodoncia", body.GetProperty("specialty").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());

        var reloaded = await ReadAsync(await _admin.GetAsync($"{Url}/{id}"));
        Assert.Equal("Luisa", reloaded.GetProperty("firstName").GetString());
        Assert.Equal(license, reloaded.GetProperty("licenseNumber").GetString());
        Assert.Equal([("Monday", "08:00:00", "12:00:00")], Schedule(reloaded));
        Assert.Equal("Luisa Vera", await UserFullNameAsync(reloaded.GetProperty("userId").GetGuid()));
    }

    [Fact]
    public async Task Update_WithoutSpecialty_ClearsIt()
    {
        var id = await RegisterAsync();

        var response = await _admin.PutAsJsonAsync($"{Url}/{id}", new { firstName = "Carla", lastName = "Mora", licenseNumber = NewLicense() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ReadAsync(response)).GetProperty("specialty").ValueKind);
    }

    [Fact]
    public async Task Update_KeepingItsOwnLicenseInOtherCase_Returns200()
    {
        var license = NewLicense();
        var id = await RegisterAsync(Registration(license: license));

        var response = await _admin.PutAsJsonAsync($"{Url}/{id}", Edit(license.ToLowerInvariant()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithTheLicenseOfAnotherDentistInOtherCase_Returns409AndChangesNothing()
    {
        var taken = NewLicense();
        await RegisterAsync(Registration(license: taken));
        var id = await RegisterAsync();

        var response = await _admin.PutAsJsonAsync($"{Url}/{id}", Edit(taken.ToLowerInvariant()));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "DUPLICATE");
        var unchanged = await ReadAsync(await _admin.GetAsync($"{Url}/{id}"));
        Assert.Equal("Carla", unchanged.GetProperty("firstName").GetString());
        Assert.Equal("Carla Mora", await UserFullNameAsync(unchanged.GetProperty("userId").GetGuid()));
    }

    [Theory]
    [InlineData("""{ "lastName": "Vera", "licenseNumber": "LIC-1" }""")]
    [InlineData("""{ "firstName": "Luisa", "lastName": " ", "licenseNumber": "LIC-1" }""")]
    [InlineData("""{ "firstName": "Luisa", "lastName": "Vera" }""")]
    [InlineData("""{ "firstName": "Luisa", "lastName": "Vera", "licenseNumber": "LIC-1234567890123456789012345678901" }""")]
    public async Task Update_WithInvalidData_Returns400Validation(string json)
    {
        var id = await RegisterAsync();

        var response = await _admin.PutAsync($"{Url}/{id}", new StringContent(json, Encoding.UTF8, "application/json"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Fact]
    public async Task Update_UnknownId_Returns404NotFound()
    {
        var response = await _admin.PutAsJsonAsync($"{Url}/{Guid.NewGuid()}", Edit(NewLicense()));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NOT-FOUND");
    }

    [Theory]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Dentist)]
    public async Task Update_WithoutAdminRole_Returns403(UserRole role)
    {
        var id = await RegisterAsync();

        var response = await factory.CreateClientWithToken(role).PutAsJsonAsync($"{Url}/{id}", Edit(NewLicense()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"{Url}/{Guid.NewGuid()}", Edit(NewLicense()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Weekly schedule

    [Fact]
    public async Task SetSchedule_AsAdmin_Returns200OrderedAndTheScheduleIsStored()
    {
        var id = await RegisterAsync();

        var response = await PutScheduleAsync(id,
            Block("Saturday", "08:00", "12:00"),
            Block("Monday", "14:00", "18:00"),
            Block("Monday", "08:00:00", "12:00:00"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<(string, string, string)> expected =
        [
            ("Monday", "08:00:00", "12:00:00"),
            ("Monday", "14:00:00", "18:00:00"),
            ("Saturday", "08:00:00", "12:00:00")
        ];
        Assert.Equal(expected, Schedule(await ReadAsync(response)));
        Assert.Equal(expected, Schedule(await ReadAsync(await _admin.GetAsync($"{Url}/{id}"))));
    }

    [Fact]
    public async Task SetSchedule_ASecondTime_ReplacesTheWholeSchedule()
    {
        var id = await RegisterAsync();
        await PutScheduleAsync(id, Block("Monday", "08:00", "12:00"), Block("Tuesday", "08:00", "12:00"));

        var response = await PutScheduleAsync(id, Block("Wednesday", "09:00", "13:00"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            [("Wednesday", "09:00:00", "13:00:00")],
            Schedule(await ReadAsync(await _admin.GetAsync($"{Url}/{id}"))));
    }

    [Fact]
    public async Task SetSchedule_WithAnEmptyArray_LeavesNoWorkingHours()
    {
        var id = await RegisterAsync();
        await PutScheduleAsync(id, Block("Monday", "08:00", "12:00"));

        var response = await PutScheduleAsync(id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(Schedule(await ReadAsync(await _admin.GetAsync($"{Url}/{id}"))));
    }

    [Fact]
    public async Task SetSchedule_BlocksThatTouch_Returns200()
    {
        var id = await RegisterAsync();

        var response = await PutScheduleAsync(id, Block("Monday", "08:00", "12:00"), Block("Monday", "12:00", "16:00"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SetSchedule_WithOverlappingBlocks_Returns400AndKeepsTheOldSchedule()
    {
        var id = await RegisterAsync();
        await PutScheduleAsync(id, Block("Friday", "08:00", "12:00"));

        var response = await PutScheduleAsync(id, Block("Monday", "08:00", "12:00"), Block("Monday", "11:00", "15:00"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
        Assert.Equal(
            [("Friday", "08:00:00", "12:00:00")],
            Schedule(await ReadAsync(await _admin.GetAsync($"{Url}/{id}"))));
    }

    [Theory]
    [InlineData("""[{ "day": "Monday", "start": "12:00", "end": "08:00" }]""")]  // ends before it starts
    [InlineData("""[{ "day": "Monday", "start": "08:00", "end": "08:00" }]""")]  // empty block
    [InlineData("""[{ "day": "Funday", "start": "08:00", "end": "12:00" }]""")]  // unknown day
    [InlineData("""[{ "day": 1, "start": "08:00", "end": "12:00" }]""")]         // day as a number
    [InlineData("""[{ "day": "Monday", "start": "8am", "end": "12:00" }]""")]    // malformed time
    [InlineData("""[{ "day": "Monday", "start": "25:00", "end": "26:00" }]""")]  // impossible time
    [InlineData("""[{ "start": "08:00", "end": "12:00" }]""")]                   // missing day
    [InlineData("""[{ "day": "Monday", "end": "12:00" }]""")]                    // missing start
    [InlineData("""[{ "day": "Monday", "start": "08:00" }]""")]                  // missing end
    [InlineData("""[null]""")]
    [InlineData("""null""")]
    [InlineData("""{ "day": "Monday", "start": "08:00", "end": "12:00" }""")]    // an object, not an array
    public async Task SetSchedule_WithInvalidData_Returns400Validation(string json)
    {
        var id = await RegisterAsync();

        var response = await PutRawScheduleAsync(id, json);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Fact]
    public async Task SetSchedule_UnknownId_Returns404NotFound()
    {
        var response = await PutScheduleAsync(Guid.NewGuid(), Block("Monday", "08:00", "12:00"));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NOT-FOUND");
    }

    [Theory]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Dentist)]
    public async Task SetSchedule_WithoutAdminRole_Returns403(UserRole role)
    {
        var id = await RegisterAsync();

        var response = await factory.CreateClientWithToken(role)
            .PutAsJsonAsync($"{Url}/{id}/schedule", new[] { Block("Monday", "08:00", "12:00") });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SetSchedule_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient()
            .PutAsJsonAsync($"{Url}/{Guid.NewGuid()}/schedule", new[] { Block("Monday", "08:00", "12:00") });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
