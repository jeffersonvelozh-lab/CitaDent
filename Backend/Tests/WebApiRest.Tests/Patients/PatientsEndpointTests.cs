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
}
