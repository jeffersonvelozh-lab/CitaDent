using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain.Patients;
using Domain.Users;

namespace WebApiRest.Tests.Patients;

/// <summary>Integration tests of /api/patients. All data is fictitious.</summary>
public class PatientsEndpointTests(CitaDentApiFactory factory) : IClassFixture<CitaDentApiFactory>
{
    private const string Url = "/api/patients";

    private readonly HttpClient _reception = factory.CreateClientWithToken(UserRole.Reception);

    /// <summary>A random cédula with a valid check digit, so each test registers its own document.</summary>
    private static string NewCedula()
    {
        var digits = new int[10];
        var province = Random.Shared.Next(1, 25);
        digits[0] = province / 10;
        digits[1] = province % 10;
        digits[2] = Random.Shared.Next(0, 6);
        for (var i = 3; i < 9; i++)
        {
            digits[i] = Random.Shared.Next(0, 10);
        }

        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var product = digits[i] * (i % 2 == 0 ? 2 : 1);
            sum += product >= 10 ? product - 9 : product;
        }

        digits[9] = (10 - sum % 10) % 10;

        var cedula = string.Concat(digits);
        Assert.True(EcuadorianId.IsValidCedula(cedula));
        return cedula;
    }

    private static object Adult(string cedula) => new
    {
        documentType = "Cedula",
        documentNumber = cedula,
        firstName = "Lucía",
        lastName = "Andrade",
        birthDate = "1990-03-15",
        phone = "0991234567"
    };

    // A ten-year-old, by today's date.
    private static string MinorBirthDate() =>
        DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10).ToString("yyyy-MM-dd");

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
    public async Task Create_AsStaff_Returns201WithLocationAndBody(UserRole role)
    {
        var cedula = NewCedula();

        var response = await factory.CreateClientWithToken(role).PostAsJsonAsync(Url, Adult(cedula));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Cedula", body.GetProperty("documentType").GetString());
        Assert.Equal(cedula, body.GetProperty("documentNumber").GetString());
        Assert.Equal("Lucía", body.GetProperty("firstName").GetString());
        Assert.Equal("1990-03-15", body.GetProperty("birthDate").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());
        Assert.NotNull(response.Headers.Location);

        var fetched = await _reception.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task Create_AsDentist_Returns403()
    {
        var response = await factory.CreateClientWithToken(UserRole.Dentist).PostAsJsonAsync(Url, Adult(NewCedula()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(Url, Adult(NewCedula()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithACedulaAlreadyRegistered_Returns409Duplicate()
    {
        var cedula = NewCedula();
        Assert.Equal(HttpStatusCode.Created, (await _reception.PostAsJsonAsync(Url, Adult(cedula))).StatusCode);

        var response = await _reception.PostAsJsonAsync(Url, Adult(cedula));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "DUPLICATE");
    }

    [Fact]
    public async Task Create_MinorWithoutGuardian_Returns422Rn12()
    {
        var response = await _reception.PostAsJsonAsync(Url, new
        {
            documentType = "Cedula",
            documentNumber = NewCedula(),
            firstName = "Mateo",
            lastName = "Andrade",
            birthDate = MinorBirthDate()
        });

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "RN-12");
    }

    [Fact]
    public async Task Create_MinorWithGuardian_Returns201()
    {
        var response = await _reception.PostAsJsonAsync(Url, new
        {
            documentType = "Cedula",
            documentNumber = NewCedula(),
            firstName = "Mateo",
            lastName = "Andrade",
            birthDate = MinorBirthDate(),
            guardianName = "Rosa Andrade",
            guardianPhone = "0987654321"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Rosa Andrade", (await ReadAsync(response)).GetProperty("guardianName").GetString());
    }

    [Fact]
    public async Task Create_WithAnInvalidCedula_Returns422Rn10()
    {
        var response = await _reception.PostAsJsonAsync(Url, Adult("0923456785"));

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "RN-10");
    }

    [Fact]
    public async Task Create_WithAPassport_Returns201WithTheNumberInUpperCase()
    {
        var passport = $"ab{Random.Shared.Next(100000, 999999)}";

        var response = await _reception.PostAsJsonAsync(Url, new
        {
            documentType = "Passport",
            documentNumber = passport,
            firstName = "John",
            lastName = "Smith",
            birthDate = "1985-07-01"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Passport", body.GetProperty("documentType").GetString());
        Assert.Equal(passport.ToUpperInvariant(), body.GetProperty("documentNumber").GetString());
    }

    [Theory]
    [InlineData("""{ "documentType": "DriverLicense", "documentNumber": "1", "firstName": "A", "lastName": "B", "birthDate": "1990-01-01" }""")]
    [InlineData("""{ "documentType": 1, "documentNumber": "0923456784", "firstName": "A", "lastName": "B", "birthDate": "1990-01-01" }""")]
    [InlineData("""{ "documentNumber": "0923456784", "firstName": "A", "lastName": "B", "birthDate": "1990-01-01" }""")]
    [InlineData("""{ "documentType": "Cedula", "documentNumber": "0923456784", "firstName": "A", "lastName": "B" }""")]
    [InlineData("""{ "documentType": "Cedula", "documentNumber": "0923456784", "firstName": "A", "lastName": "B", "birthDate": "15/03/1990" }""")]
    public async Task Create_WithAMissingOrMalformedField_Returns400Validation(string json)
    {
        var response = await _reception.PostAsync(Url, new StringContent(json, Encoding.UTF8, "application/json"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Fact]
    public async Task Create_WithoutFirstName_Returns400Validation()
    {
        var response = await _reception.PostAsJsonAsync(Url, new
        {
            documentType = "Cedula",
            documentNumber = NewCedula(),
            lastName = "Andrade",
            birthDate = "1990-03-15"
        });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    // Get

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Dentist)]
    public async Task Get_WorksForEveryRole(UserRole role)
    {
        var created = await ReadAsync(await _reception.PostAsJsonAsync(Url, Adult(NewCedula())));
        var id = created.GetProperty("id").GetGuid();

        var response = await factory.CreateClientWithToken(role).GetAsync($"{Url}/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(id, (await ReadAsync(response)).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Get_UnknownId_Returns404NotFound()
    {
        var response = await _reception.GetAsync($"{Url}/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NOT-FOUND");
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync($"{Url}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Helpers for search and update. The database is shared by the whole class, so each search test
    // puts a unique mark (letters only) in the last names it creates and searches for it.

    private static string NewMark() =>
        "zz" + new string(Guid.NewGuid().ToString("N").Select(c => (char)('a' + (c % 26))).Take(10).ToArray());

    private async Task<Guid> CreatePatientAsync(string firstName, string lastName)
    {
        var response = await _reception.PostAsJsonAsync(Url, new
        {
            documentType = "Cedula",
            documentNumber = NewCedula(),
            firstName,
            lastName,
            birthDate = "1990-03-15"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private static Guid[] Ids(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToArray();

    // Search

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Dentist)]
    public async Task Search_ByPartOfTheName_WorksForEveryRole(UserRole role)
    {
        var mark = NewMark();
        var ana = await CreatePatientAsync("Ana", "Mora" + mark);
        await CreatePatientAsync("Luis", "Mora" + mark);

        var response = await factory.CreateClientWithToken(role).GetAsync($"{Url}?term=AN%20{mark[..8]}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal([ana], Ids(body));
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(20, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, body.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Search_ByPartOfTheDocument_FindsThePatient()
    {
        var cedula = NewCedula();
        var created = await ReadAsync(await _reception.PostAsJsonAsync(Url, Adult(cedula)));

        var body = await ReadAsync(await _reception.GetAsync($"{Url}?term={cedula}"));

        Assert.Contains(created.GetProperty("id").GetGuid(), Ids(body));
    }

    [Fact]
    public async Task Search_WithNoMatches_Returns200WithAnEmptyPage()
    {
        var response = await _reception.GetAsync($"{Url}?term={NewMark()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Empty(Ids(body));
        Assert.Equal(0, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, body.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Search_APageBeyondTheEnd_Returns200WithNoItemsButTheTotal()
    {
        var mark = NewMark();
        await CreatePatientAsync("Ana", mark);
        await CreatePatientAsync("Luis", mark);

        var response = await _reception.GetAsync($"{Url}?term={mark}&page=3&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Empty(Ids(body));
        Assert.Equal(3, body.GetProperty("page").GetInt32());
        Assert.Equal(2, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, body.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Search_PagesInOrderByLastName()
    {
        var mark = NewMark();
        var second = await CreatePatientAsync("Ana", mark + "b");
        var first = await CreatePatientAsync("Luis", mark + "a");

        var page1 = await ReadAsync(await _reception.GetAsync($"{Url}?term={mark}&page=1&pageSize=1"));
        var page2 = await ReadAsync(await _reception.GetAsync($"{Url}?term={mark}&page=2&pageSize=1"));

        Assert.Equal([first], Ids(page1));
        Assert.Equal([second], Ids(page2));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=abc")]
    [InlineData("includeInactive=maybe")]
    public async Task Search_WithAnInvalidQuery_Returns400Validation(string query)
    {
        var response = await _reception.GetAsync($"{Url}?{query}");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Fact]
    public async Task Search_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync($"{Url}?term=ana");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Update

    [Theory]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Admin)]
    public async Task Update_AsStaff_Returns200AndTheChangeIsStored(UserRole role)
    {
        var id = await CreatePatientAsync("Lucía", "Andrade");
        var passport = $"CD{Random.Shared.Next(100000, 999999)}";

        var response = await factory.CreateClientWithToken(role).PutAsJsonAsync($"{Url}/{id}", new
        {
            documentType = "Passport",
            documentNumber = passport.ToLowerInvariant(),
            firstName = "Lucía Fernanda",
            lastName = "Andrade Mora",
            birthDate = "1990-03-16",
            email = "lucia@example.com"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal("Passport", body.GetProperty("documentType").GetString());
        Assert.Equal(passport, body.GetProperty("documentNumber").GetString());

        var fetched = await ReadAsync(await _reception.GetAsync($"{Url}/{id}"));
        Assert.Equal("Lucía Fernanda", fetched.GetProperty("firstName").GetString());
        Assert.Equal("Andrade Mora", fetched.GetProperty("lastName").GetString());
        Assert.Equal("1990-03-16", fetched.GetProperty("birthDate").GetString());
        Assert.Equal("lucia@example.com", fetched.GetProperty("email").GetString());
        Assert.Equal(JsonValueKind.Null, fetched.GetProperty("phone").ValueKind); // Left out, so cleared.
    }

    [Fact]
    public async Task Update_KeepingItsOwnCedula_Returns200()
    {
        var cedula = NewCedula();
        var id = (await ReadAsync(await _reception.PostAsJsonAsync(Url, Adult(cedula)))).GetProperty("id").GetGuid();

        var response = await _reception.PutAsJsonAsync($"{Url}/{id}", Adult(cedula));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_ToTheCedulaOfAnotherPatient_Returns409Duplicate()
    {
        var takenCedula = NewCedula();
        Assert.Equal(HttpStatusCode.Created, (await _reception.PostAsJsonAsync(Url, Adult(takenCedula))).StatusCode);
        var id = await CreatePatientAsync("Luis", "Mora");

        var response = await _reception.PutAsJsonAsync($"{Url}/{id}", Adult(takenCedula));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "DUPLICATE");
    }

    [Fact]
    public async Task Update_WithAnInvalidCedula_Returns422Rn10()
    {
        var id = await CreatePatientAsync("Lucía", "Andrade");

        var response = await _reception.PutAsJsonAsync($"{Url}/{id}", Adult("0923456785"));

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "RN-10");
    }

    [Fact]
    public async Task Update_ToAMinorWithoutGuardian_Returns422Rn12AndChangesNothing()
    {
        var id = await CreatePatientAsync("Lucía", "Andrade");

        var response = await _reception.PutAsJsonAsync($"{Url}/{id}", new
        {
            documentType = "Cedula",
            documentNumber = NewCedula(),
            firstName = "Mateo",
            lastName = "Andrade",
            birthDate = MinorBirthDate()
        });

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "RN-12");
        var fetched = await ReadAsync(await _reception.GetAsync($"{Url}/{id}"));
        Assert.Equal("Lucía", fetched.GetProperty("firstName").GetString());
    }

    [Fact]
    public async Task Update_WithoutBirthDate_Returns400Validation()
    {
        var id = await CreatePatientAsync("Lucía", "Andrade");

        var response = await _reception.PutAsJsonAsync($"{Url}/{id}", new
        {
            documentType = "Cedula",
            documentNumber = NewCedula(),
            firstName = "Lucía",
            lastName = "Andrade"
        });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Fact]
    public async Task Update_UnknownId_Returns404NotFound()
    {
        var response = await _reception.PutAsJsonAsync($"{Url}/{Guid.NewGuid()}", Adult(NewCedula()));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NOT-FOUND");
    }

    [Fact]
    public async Task Update_AsDentist_Returns403()
    {
        var id = await CreatePatientAsync("Lucía", "Andrade");

        var response = await factory.CreateClientWithToken(UserRole.Dentist)
            .PutAsJsonAsync($"{Url}/{id}", Adult(NewCedula()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"{Url}/{Guid.NewGuid()}", Adult(NewCedula()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Activate and deactivate (RN-11: deactivate instead of delete)

    [Theory]
    [InlineData(UserRole.Reception)]
    [InlineData(UserRole.Admin)]
    public async Task DeactivateAndActivate_AsStaff_Return204AndChangeTheState(UserRole role)
    {
        var mark = NewMark();
        var id = await CreatePatientAsync("Ana", mark);
        var client = factory.CreateClientWithToken(role);

        var deactivated = await client.PostAsync($"{Url}/{id}/deactivate", content: null);

        Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);
        var fetched = await ReadAsync(await _reception.GetAsync($"{Url}/{id}"));
        Assert.False(fetched.GetProperty("isActive").GetBoolean()); // Still there: deactivated, not deleted.
        Assert.Empty(Ids(await ReadAsync(await _reception.GetAsync($"{Url}?term={mark}"))));
        Assert.Equal(
            [id], Ids(await ReadAsync(await _reception.GetAsync($"{Url}?term={mark}&includeInactive=true"))));

        var activated = await client.PostAsync($"{Url}/{id}/activate", content: null);

        Assert.Equal(HttpStatusCode.NoContent, activated.StatusCode);
        Assert.True((await ReadAsync(await _reception.GetAsync($"{Url}/{id}"))).GetProperty("isActive").GetBoolean());
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task SetActive_UnknownId_Returns404NotFound(string action)
    {
        var response = await _reception.PostAsync($"{Url}/{Guid.NewGuid()}/{action}", content: null);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NOT-FOUND");
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task SetActive_AsDentist_Returns403(string action)
    {
        var id = await CreatePatientAsync("Ana", "Mora");

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
        // RN-11: a patient is deactivated, never deleted, so the API has no DELETE.
        var id = await CreatePatientAsync("Ana", "Mora");

        var response = await _reception.DeleteAsync($"{Url}/{id}");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _reception.GetAsync($"{Url}/{id}")).StatusCode);
    }
}
