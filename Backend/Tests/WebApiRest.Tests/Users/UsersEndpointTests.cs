using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain.Users;
using Microsoft.IdentityModel.JsonWebTokens;

namespace WebApiRest.Tests.Users;

/// <summary>
/// Integration tests of /api/users: only Admin. The last-admin rule needs a database with a known set of
/// admins, so it lives in <see cref="LastAdminEndpointTests"/>; no test here deactivates or demotes the
/// seeded admin. All data is fictitious.
/// </summary>
public class UsersEndpointTests(CitaDentApiFactory factory) : IClassFixture<CitaDentApiFactory>
{
    private const string Url = "/api/users";
    private const string Password = "Clave-Usuario-2026";
    private const string NewPassword = "Clave-Nueva-2026!";

    private readonly HttpClient _admin = factory.CreateClientWithToken(UserRole.Admin);

    // Emails are unique, and the database is shared by the tests of this class.
    private static string NewEmail() => $"user-{Guid.NewGuid():N}@citadent.test";

    private static object Account(string? email = null, string role = "Reception", string password = Password) => new
    {
        email = email ?? NewEmail(),
        fullName = "Ana Torres",
        password,
        role
    };

    private async Task<(Guid Id, string Email)> CreateAsync(string role = "Reception")
    {
        var email = NewEmail();
        var response = await _admin.PostAsJsonAsync(Url, Account(email, role));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await ReadAsync(response)).GetProperty("id").GetGuid(), email);
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password = Password) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });

    private Task<HttpResponseMessage> PostRawAsync(string url, string json) =>
        _admin.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));

    private Task<HttpResponseMessage> PutRawAsync(string url, string json) =>
        _admin.PutAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));

    private async Task<Guid> IdOfAsync(string email)
    {
        var all = await ReadAsync(await _admin.GetAsync($"{Url}?includeInactive=true"));
        return all.EnumerateArray().Single(u => u.GetProperty("email").GetString() == email).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await ReadAsync(response)).GetProperty("code").GetString());
    }

    // Create

    [Theory]
    [InlineData("Reception")]
    [InlineData("Admin")]
    public async Task Create_AsAdmin_Returns201WithLocationAndNoPassword(string role)
    {
        var email = NewEmail();

        var response = await _admin.PostAsJsonAsync(Url, Account(email.ToUpperInvariant(), role));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var body = await ReadAsync(response);
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal("Ana Torres", body.GetProperty("fullName").GetString());
        Assert.Equal(role, body.GetProperty("role").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, json, StringComparison.Ordinal);

        Assert.NotNull(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, (await _admin.GetAsync(response.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email)).StatusCode);
    }

    [Fact]
    public async Task Create_WithAnEmailAlreadyUsed_Returns409Duplicate()
    {
        var response = await _admin.PostAsJsonAsync(Url, Account(CitaDentApiFactory.ActiveEmail.ToUpperInvariant()));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "DUPLICATE");
    }

    [Theory]
    [InlineData("""{ "email": "ana@citadent.test", "fullName": "Ana Torres", "password": "corta", "role": "Reception" }""")]
    [InlineData("""{ "email": "no-es-email", "fullName": "Ana Torres", "password": "Clave-Usuario-2026", "role": "Reception" }""")]
    [InlineData("""{ "email": "ana@citadent.test", "fullName": " ", "password": "Clave-Usuario-2026", "role": "Reception" }""")]
    [InlineData("""{ "email": "ana@citadent.test", "fullName": "Ana Torres", "password": "Clave-Usuario-2026" }""")]
    [InlineData("""{ "email": "ana@citadent.test", "fullName": "Ana Torres", "password": "Clave-Usuario-2026", "role": "Dentist" }""")]
    [InlineData("""{ "email": "ana@citadent.test", "fullName": "Ana Torres", "password": "Clave-Usuario-2026", "role": 2 }""")]
    [InlineData("""{ "email": "ana@citadent.test", "fullName": "Ana Torres", "password": "Clave-Usuario-2026", "role": "Jefe" }""")]
    public async Task Create_WithInvalidData_Returns400Validation(string json)
    {
        var response = await PostRawAsync(Url, json);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    // List and get

    [Fact]
    public async Task List_FiltersByStateAndRole()
    {
        var (receptionId, _) = await CreateAsync("Reception");
        var (adminId, _) = await CreateAsync("Admin");
        var (inactiveId, _) = await CreateAsync("Reception");
        Assert.Equal(HttpStatusCode.NoContent, (await _admin.PostAsync($"{Url}/{inactiveId}/deactivate", null)).StatusCode);

        static List<Guid> Ids(JsonElement list) => list.EnumerateArray().Select(u => u.GetProperty("id").GetGuid()).ToList();

        var actives = Ids(await ReadAsync(await _admin.GetAsync(Url)));
        var all = Ids(await ReadAsync(await _admin.GetAsync($"{Url}?includeInactive=true")));
        var receptionList = await ReadAsync(await _admin.GetAsync($"{Url}?includeInactive=true&role=reception"));

        Assert.Contains(receptionId, actives);
        Assert.Contains(adminId, actives);
        Assert.DoesNotContain(inactiveId, actives);
        Assert.Contains(inactiveId, all);
        Assert.All(receptionList.EnumerateArray(), u => Assert.Equal("Reception", u.GetProperty("role").GetString()));
        Assert.Contains(inactiveId, Ids(receptionList));
        Assert.DoesNotContain(adminId, Ids(receptionList));
    }

    [Fact]
    public async Task List_IncludesDentistAccounts_WithoutHashes()
    {
        var response = await _admin.GetAsync($"{Url}?includeInactive=true&role=Dentist");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains(CitaDentApiFactory.InactiveEmail, json, StringComparison.Ordinal);
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("99")]
    [InlineData("Jefe")]
    public async Task List_WithAnUnknownRole_Returns400Validation(string role)
    {
        var response = await _admin.GetAsync($"{Url}?role={role}");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Fact]
    public async Task Get_UnknownId_Returns404NotFound()
    {
        var response = await _admin.GetAsync($"{Url}/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NOT-FOUND");
    }

    // Email

    [Fact]
    public async Task ChangeEmail_SignInWorksWithTheNewEmailAndFailsWithTheOldOne()
    {
        var (id, oldEmail) = await CreateAsync();
        var newEmail = NewEmail();

        var response = await _admin.PutAsJsonAsync($"{Url}/{id}/email", new { email = newEmail.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(newEmail, (await ReadAsync(response)).GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(newEmail)).StatusCode);
        await AssertProblemAsync(await LoginAsync(oldEmail), HttpStatusCode.Unauthorized, "AUTH-INVALID-CREDENTIALS");
    }

    [Fact]
    public async Task ChangeEmail_ToTheEmailOfAnotherUser_Returns409Duplicate()
    {
        var (id, _) = await CreateAsync();

        var response = await _admin.PutAsJsonAsync($"{Url}/{id}/email", new { email = CitaDentApiFactory.ActiveEmail });

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "DUPLICATE");
    }

    [Theory]
    [InlineData("""{ "email": "no-es-email" }""")]
    [InlineData("""{ }""")]
    public async Task ChangeEmail_WithAnInvalidEmail_Returns400Validation(string json)
    {
        var (id, _) = await CreateAsync();

        var response = await PutRawAsync($"{Url}/{id}/email", json);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    // Full name

    [Fact]
    public async Task ChangeFullName_Returns200_AndTheNextTokenCarriesTheNewName()
    {
        var (id, email) = await CreateAsync();

        var response = await _admin.PutAsJsonAsync($"{Url}/{id}/full-name", new { fullName = "  Ana María Torres " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Ana María Torres", (await ReadAsync(response)).GetProperty("fullName").GetString());
        var reloaded = await ReadAsync(await _admin.GetAsync($"{Url}/{id}"));
        Assert.Equal("Ana María Torres", reloaded.GetProperty("fullName").GetString());

        var login = await ReadAsync(await LoginAsync(email));
        var token = new JsonWebTokenHandler().ReadJsonWebToken(login.GetProperty("accessToken").GetString());
        Assert.Equal("Ana María Torres", token.GetClaim(JwtRegisteredClaimNames.Name).Value);
    }

    [Theory]
    [InlineData("""{ "fullName": "  " }""")]
    [InlineData("""{ }""")]
    public async Task ChangeFullName_WithABlankName_Returns400Validation(string json)
    {
        var (id, _) = await CreateAsync();

        var response = await PutRawAsync($"{Url}/{id}/full-name", json);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Fact]
    public async Task ChangeFullName_WithATooLongName_Returns400Validation()
    {
        var (id, _) = await CreateAsync();

        var response = await _admin.PutAsJsonAsync($"{Url}/{id}/full-name", new { fullName = new string('A', User.FullNameMaxLength + 1) });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Fact]
    public async Task ChangeFullName_OfADentistAccount_Returns400Validation()
    {
        var id = await IdOfAsync(CitaDentApiFactory.InactiveEmail);

        var response = await _admin.PutAsJsonAsync($"{Url}/{id}/full-name", new { fullName = "Otro Nombre" });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    // Password

    [Fact]
    public async Task ResetPassword_Returns204_SignInWorksWithTheNewPasswordAndFailsWithTheOldOne()
    {
        var (id, email) = await CreateAsync();

        var response = await _admin.PutAsJsonAsync($"{Url}/{id}/password", new { password = NewPassword });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email, NewPassword)).StatusCode);
        await AssertProblemAsync(await LoginAsync(email, Password), HttpStatusCode.Unauthorized, "AUTH-INVALID-CREDENTIALS");
    }

    [Theory]
    [InlineData("""{ "password": "corta-11-ch" }""")]
    [InlineData("""{ }""")]
    public async Task ResetPassword_WithAShortPassword_Returns400AndKeepsTheOldOne(string json)
    {
        var (id, email) = await CreateAsync();

        var response = await PutRawAsync($"{Url}/{id}/password", json);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email)).StatusCode);
    }

    // Role

    [Fact]
    public async Task ChangeRole_PromotesReceptionToAdmin_AndTheNextTokenHasTheNewRole()
    {
        var (id, email) = await CreateAsync("Reception");

        var response = await _admin.PutAsJsonAsync($"{Url}/{id}/role", new { role = "Admin" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Admin", (await ReadAsync(response)).GetProperty("role").GetString());
        Assert.Equal("Admin", (await ReadAsync(await LoginAsync(email))).GetProperty("role").GetString());
    }

    [Theory]
    [InlineData("""{ "role": "Dentist" }""")]
    [InlineData("""{ "role": 1 }""")]
    [InlineData("""{ }""")]
    public async Task ChangeRole_WithAnInvalidRole_Returns400Validation(string json)
    {
        var (id, _) = await CreateAsync("Reception");

        var response = await PutRawAsync($"{Url}/{id}/role", json);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Fact]
    public async Task ChangeRole_OfADentistAccount_Returns400Validation()
    {
        var id = await IdOfAsync(CitaDentApiFactory.InactiveEmail);

        var response = await _admin.PutAsJsonAsync($"{Url}/{id}/role", new { role = "Reception" });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    // Activate and deactivate

    [Fact]
    public async Task DeactivateAndActivate_Return204_AndControlSignIn()
    {
        var (id, email) = await CreateAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await _admin.PostAsync($"{Url}/{id}/deactivate", null)).StatusCode);
        Assert.False((await ReadAsync(await _admin.GetAsync($"{Url}/{id}"))).GetProperty("isActive").GetBoolean());
        await AssertProblemAsync(await LoginAsync(email), HttpStatusCode.Unauthorized, "AUTH-INVALID-CREDENTIALS");

        Assert.Equal(HttpStatusCode.NoContent, (await _admin.PostAsync($"{Url}/{id}/activate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email)).StatusCode);
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task SetActive_OnADentistAccount_Returns400Validation(string action)
    {
        var id = await IdOfAsync(CitaDentApiFactory.InactiveEmail);

        var response = await _admin.PostAsync($"{Url}/{id}/{action}", null);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    // Not found, authentication and roles, for every endpoint

    public static TheoryData<string, string, string?> Endpoints(string id) => new()
    {
        { "GET", Url, null },
        { "GET", $"{Url}/{id}", null },
        { "POST", Url, """{ "email": "x@citadent.test", "fullName": "X", "password": "Clave-Usuario-2026", "role": "Reception" }""" },
        { "PUT", $"{Url}/{id}/email", """{ "email": "x@citadent.test" }""" },
        { "PUT", $"{Url}/{id}/full-name", """{ "fullName": "Ana Torres" }""" },
        { "PUT", $"{Url}/{id}/password", """{ "password": "Clave-Usuario-2026" }""" },
        { "PUT", $"{Url}/{id}/role", """{ "role": "Admin" }""" },
        { "POST", $"{Url}/{id}/activate", null },
        { "POST", $"{Url}/{id}/deactivate", null },
    };

    public static TheoryData<string, string, string?> AnyEndpoint => Endpoints(Guid.NewGuid().ToString());

    public static TheoryData<string, string, string?> EndpointsWithId
    {
        get
        {
            var data = new TheoryData<string, string, string?>();
            foreach (var row in Endpoints(Guid.NewGuid().ToString()))
            {
                var (method, url, body) = ((string)row[0], (string)row[1], (string?)row[2]);
                if (url != Url)
                {
                    data.Add(method, url, body);
                }
            }

            return data;
        }
    }

    private static HttpRequestMessage Request(string method, string url, string? body) =>
        new(new HttpMethod(method), url)
        {
            Content = body is null ? null : new StringContent(body, Encoding.UTF8, "application/json")
        };

    [Theory]
    [MemberData(nameof(EndpointsWithId))]
    public async Task EveryEndpointWithAnId_UnknownId_Returns404NotFound(string method, string url, string? body)
    {
        var response = await _admin.SendAsync(Request(method, url, body));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NOT-FOUND");
    }

    [Theory]
    [MemberData(nameof(AnyEndpoint))]
    public async Task EveryEndpoint_WithoutToken_Returns401(string method, string url, string? body)
    {
        var response = await factory.CreateClient().SendAsync(Request(method, url, body));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AnyEndpoint))]
    public async Task EveryEndpoint_WithoutAdminRole_Returns403(string method, string url, string? body)
    {
        foreach (var role in new[] { UserRole.Reception, UserRole.Dentist })
        {
            var response = await factory.CreateClientWithToken(role).SendAsync(Request(method, url, body));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
