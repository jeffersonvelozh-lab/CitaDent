using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain.Appointments;
using Domain.Patients;
using Domain.Procedures;
using Domain.Scheduling;
using Domain.Users;

namespace WebApiRest.Tests.Appointments;

/// <summary>
/// Integration tests of cancel, no-show and complete (RN-07, RN-08, RN-09), by role and by starting state.
/// The API runs on the real clock and refuses to book in the past (RN-06), so each test saves its own
/// appointment straight into the database: one two days ago, or one in 2030. All data is fictitious.
/// </summary>
public class AppointmentStatusEndpointTests(CitaDentApiFactory factory) : IClassFixture<CitaDentApiFactory>
{
    private const string Url = "/api/appointments";

    private static readonly TimeSpan Ecuador = TimeSpan.FromHours(-5);
    private static readonly TimeZoneInfo EcuadorZone =
        TimeZoneInfo.CreateCustomTimeZone("ecuador-test", Ecuador, "Ecuador", "Ecuador");

    private readonly HttpClient _reception = factory.CreateClientForUser(factory.ActiveUser);
    private readonly HttpClient _admin = factory.CreateClientWithToken(UserRole.Admin);

    /// <summary>A saved appointment and the account of its dentist, to sign in as them.</summary>
    private sealed record Booked(Guid Id, User DentistUser, Appointment Appointment);

    // 10:00 at the clinic, two days ago: already over.
    private static DateTimeOffset TwoDaysAgo()
    {
        var today = DateTimeOffset.UtcNow.ToOffset(Ecuador).Date;
        return new DateTimeOffset(today.AddDays(-2).AddHours(10), Ecuador);
    }

    // Monday, December 2, 2030, 10:00 at the clinic.
    private static readonly DateTimeOffset InTheFuture = new(2030, 12, 2, 10, 0, 0, Ecuador);

    /// <summary>
    /// Saves a patient, a dentist who works every day 08:00-18:00 (with their user), a chair, a 30-minute procedure
    /// and a scheduled appointment at <paramref name="start"/>, booked by reception.
    /// </summary>
    private Booked Book(DateTimeOffset start)
    {
        var dentistUser = User.Create($"dentist-{Guid.NewGuid():N}@citadent.test", "Carla Mora", "test-hash", UserRole.Dentist);
        var dentist = Dentist.Create(dentistUser.Id, "Carla", "Mora", $"LIC-{Guid.NewGuid():N}"[..20]);
        dentist.SetWeeklySchedule(Enum.GetValues<DayOfWeek>()
            .Select(day => new WorkingHours(day, new TimeOnly(8, 0), new TimeOnly(18, 0)))
            .ToList());

        var patient = Patient.Create(
            DocumentType.Passport, $"P{Guid.NewGuid():N}"[..12].ToUpperInvariant(), "Ana", "Pérez",
            new DateOnly(1990, 5, 20), new DateOnly(2026, 11, 18));
        var chair = Chair.Create($"Sillon {Guid.NewGuid():N}");
        var procedure = Procedure.Create($"Limpieza {Guid.NewGuid():N}", null, 30, 25m);

        var appointment = Appointment.Schedule(
            patient, dentist, chair, procedure, start, factory.ActiveUser.Id, new SchedulingContext(EcuadorZone, [], []));

        factory.SaveInDatabase(dentistUser, dentist, patient, chair, procedure, appointment);
        return new Booked(appointment.Id, dentistUser, appointment);
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await ReadAsync(response)).GetProperty("code").GetString());
    }

    private static async Task AssertStatusAsync(HttpResponseMessage response, string status)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(status, (await ReadAsync(response)).GetProperty("status").GetString());
    }

    private async Task<string?> StatusOfAsync(Guid id) =>
        (await ReadAsync(await _admin.GetAsync($"{Url}/{id}"))).GetProperty("status").GetString();

    private static Task<HttpResponseMessage> CancelAsync(HttpClient client, Guid id, string reason = "El paciente viajó") =>
        client.PostAsJsonAsync($"{Url}/{id}/cancel", new { reason });

    private static Task<HttpResponseMessage> MarkNoShowAsync(HttpClient client, Guid id) =>
        client.PostAsync($"{Url}/{id}/no-show", content: null);

    private static Task<HttpResponseMessage> CompleteAsync(HttpClient client, Guid id) =>
        client.PostAsync($"{Url}/{id}/complete", content: null);

    // Cancel

    [Fact]
    public async Task Cancel_AsReception_BeforeItStarts_Returns200WithTheReason()
    {
        var booked = Book(InTheFuture);

        var response = await CancelAsync(_reception, booked.Id, "  El paciente viajó ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Cancelled", body.GetProperty("status").GetString());
        Assert.Equal("El paciente viajó", body.GetProperty("cancelReason").GetString());
        Assert.Equal("Cancelled", await StatusOfAsync(booked.Id));
    }

    [Fact]
    public async Task Cancel_AsAdmin_Returns200()
    {
        await AssertStatusAsync(await CancelAsync(_admin, Book(InTheFuture).Id), "Cancelled");
    }

    [Fact]
    public async Task Cancel_FreesTheTime_SoAnotherAppointmentCanBeBookedThere_RN08()
    {
        var booked = Book(InTheFuture);
        var a = booked.Appointment;
        var again = new
        {
            patientId = a.PatientId,
            dentistId = a.DentistId,
            chairId = a.ChairId,
            procedureId = a.ProcedureId,
            startsAt = "2030-12-02T10:00:00-05:00"
        };
        await AssertProblemAsync(await _reception.PostAsJsonAsync(Url, again), HttpStatusCode.Conflict, "RN-02");

        Assert.Equal(HttpStatusCode.OK, (await CancelAsync(_reception, booked.Id)).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await _reception.PostAsJsonAsync(Url, again)).StatusCode);
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "reason": null }""")]
    [InlineData("""{ "reason": "   " }""")]
    public async Task Cancel_WithoutAReason_Returns422_RN08(string json)
    {
        var booked = Book(InTheFuture);

        var response = await _reception.PostAsync($"{Url}/{booked.Id}/cancel", Json(json));

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "RN-08");
        Assert.Equal("Scheduled", await StatusOfAsync(booked.Id));
    }

    [Fact]
    public async Task Cancel_OnceItHasStarted_Returns400Validation()
    {
        var booked = Book(TwoDaysAgo());

        await AssertProblemAsync(await CancelAsync(_reception, booked.Id), HttpStatusCode.BadRequest, "VALIDATION");
        Assert.Equal("Scheduled", await StatusOfAsync(booked.Id));
    }

    [Fact]
    public async Task Cancel_AsDentist_Returns403()
    {
        var booked = Book(InTheFuture);

        var response = await CancelAsync(factory.CreateClientForUser(booked.DentistUser), booked.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Scheduled", await StatusOfAsync(booked.Id));
    }

    // No-show

    [Fact]
    public async Task MarkNoShow_AsReception_OnceItHasStarted_Returns200()
    {
        var booked = Book(TwoDaysAgo());

        await AssertStatusAsync(await MarkNoShowAsync(_reception, booked.Id), "NoShow");
        Assert.Equal("NoShow", await StatusOfAsync(booked.Id));
    }

    [Fact]
    public async Task MarkNoShow_AsAdmin_Returns200()
    {
        await AssertStatusAsync(await MarkNoShowAsync(_admin, Book(TwoDaysAgo()).Id), "NoShow");
    }

    [Fact]
    public async Task MarkNoShow_BeforeItStarts_Returns400Validation()
    {
        var booked = Book(InTheFuture);

        await AssertProblemAsync(await MarkNoShowAsync(_reception, booked.Id), HttpStatusCode.BadRequest, "VALIDATION");
    }

    [Fact]
    public async Task MarkNoShow_AsDentist_Returns403()
    {
        var booked = Book(TwoDaysAgo());

        var response = await MarkNoShowAsync(factory.CreateClientForUser(booked.DentistUser), booked.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Complete and RN-09

    [Fact]
    public async Task Complete_AsTheAssignedDentist_Returns200()
    {
        var booked = Book(TwoDaysAgo());

        await AssertStatusAsync(await CompleteAsync(factory.CreateClientForUser(booked.DentistUser), booked.Id), "Completed");
        Assert.Equal("Completed", await StatusOfAsync(booked.Id));
    }

    [Fact]
    public async Task Complete_AsAdmin_Returns200()
    {
        await AssertStatusAsync(await CompleteAsync(_admin, Book(TwoDaysAgo()).Id), "Completed");
    }

    [Fact]
    public async Task Complete_AsAnotherDentist_Returns403_RN09()
    {
        var booked = Book(TwoDaysAgo());
        var otherDentist = Book(TwoDaysAgo()).DentistUser;

        var response = await CompleteAsync(factory.CreateClientForUser(otherDentist), booked.Id);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "RN-09");
        Assert.Equal("Scheduled", await StatusOfAsync(booked.Id));
    }

    [Fact]
    public async Task Complete_AsReception_Returns403()
    {
        var booked = Book(TwoDaysAgo());

        Assert.Equal(HttpStatusCode.Forbidden, (await CompleteAsync(_reception, booked.Id)).StatusCode);
        Assert.Equal("Scheduled", await StatusOfAsync(booked.Id));
    }

    [Fact]
    public async Task Complete_BeforeItStarts_Returns400Validation()
    {
        var booked = Book(InTheFuture);

        var response = await CompleteAsync(factory.CreateClientForUser(booked.DentistUser), booked.Id);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION");
    }

    // RN-07: a final state never changes

    [Fact]
    public async Task ACancelledAppointment_CannotBeCancelledOrCompletedAgain_Returns409_RN07()
    {
        var booked = Book(InTheFuture);
        Assert.Equal(HttpStatusCode.OK, (await CancelAsync(_reception, booked.Id)).StatusCode);

        await AssertProblemAsync(await CancelAsync(_reception, booked.Id), HttpStatusCode.Conflict, "RN-07");
        await AssertProblemAsync(await MarkNoShowAsync(_reception, booked.Id), HttpStatusCode.Conflict, "RN-07");
        await AssertProblemAsync(
            await CompleteAsync(factory.CreateClientForUser(booked.DentistUser), booked.Id), HttpStatusCode.Conflict, "RN-07");
        Assert.Equal("Cancelled", await StatusOfAsync(booked.Id));
    }

    [Fact]
    public async Task ACompletedAppointment_CannotBeMarkedNoShowOrCancelled_Returns409_RN07()
    {
        var booked = Book(TwoDaysAgo());
        Assert.Equal(HttpStatusCode.OK, (await CompleteAsync(_admin, booked.Id)).StatusCode);

        await AssertProblemAsync(await MarkNoShowAsync(_reception, booked.Id), HttpStatusCode.Conflict, "RN-07");
        await AssertProblemAsync(await CancelAsync(_reception, booked.Id), HttpStatusCode.Conflict, "RN-07");
        await AssertProblemAsync(await CompleteAsync(_admin, booked.Id), HttpStatusCode.Conflict, "RN-07");
        Assert.Equal("Completed", await StatusOfAsync(booked.Id));
    }

    [Fact]
    public async Task ANoShowAppointment_CannotBeCompleted_Returns409_RN07()
    {
        var booked = Book(TwoDaysAgo());
        Assert.Equal(HttpStatusCode.OK, (await MarkNoShowAsync(_reception, booked.Id)).StatusCode);

        await AssertProblemAsync(
            await CompleteAsync(factory.CreateClientForUser(booked.DentistUser), booked.Id), HttpStatusCode.Conflict, "RN-07");
        Assert.Equal("NoShow", await StatusOfAsync(booked.Id));
    }

    // Unknown appointment and no token

    [Fact]
    public async Task EveryAction_WithAnUnknownId_Returns404NotFound()
    {
        var unknown = Guid.CreateVersion7();

        await AssertProblemAsync(await CancelAsync(_admin, unknown), HttpStatusCode.NotFound, "NOT-FOUND");
        await AssertProblemAsync(await MarkNoShowAsync(_admin, unknown), HttpStatusCode.NotFound, "NOT-FOUND");
        await AssertProblemAsync(await CompleteAsync(_admin, unknown), HttpStatusCode.NotFound, "NOT-FOUND");
    }

    [Fact]
    public async Task EveryAction_WithoutToken_Returns401()
    {
        var anonymous = factory.CreateClient();
        var id = Guid.CreateVersion7();

        Assert.Equal(HttpStatusCode.Unauthorized, (await CancelAsync(anonymous, id)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await MarkNoShowAsync(anonymous, id)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CompleteAsync(anonymous, id)).StatusCode);
    }
}
