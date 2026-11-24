using System.Net;
using System.Text;
using System.Text.Json;
using WebApiRest.Tests.Fakes;

namespace WebApiRest.Tests.Errors;

/// <summary>Integration tests: exceptions thrown by an endpoint come back as ProblemDetails with a code.</summary>
public class ErrorHandlingTests(CitaDentApiFactory factory) : IClassFixture<CitaDentApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("VALIDATION", HttpStatusCode.BadRequest)]
    [InlineData("RN-09", HttpStatusCode.Forbidden)]
    [InlineData("RN-02", HttpStatusCode.Conflict)]
    [InlineData("RN-12", HttpStatusCode.UnprocessableEntity)]
    [InlineData("RN-99", HttpStatusCode.UnprocessableEntity)]
    public async Task DomainException_BecomesProblemDetailsWithItsCode(string code, HttpStatusCode expected)
    {
        var response = await _client.GetAsync($"/test/errors/domain/{code}");

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var body = await ReadJson(response);
        var root = body.RootElement;
        Assert.Equal(code, root.GetProperty("code").GetString());
        Assert.Equal((int)expected, root.GetProperty("status").GetInt32());
        Assert.Equal(ErrorProbeController.DomainMessage, root.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrEmpty(root.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task UnexpectedException_Becomes500WithoutLeakingItsMessage()
    {
        var response = await _client.GetAsync("/test/errors/unexpected");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secreto", text);
        Assert.DoesNotContain(nameof(InvalidOperationException), text);

        using var body = JsonDocument.Parse(text);
        Assert.Equal("INTERNAL", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("An unexpected error occurred.", body.RootElement.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("traceId").GetString()));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{ \"number\": \"not a number\" }")]
    public async Task InvalidBody_Returns400WithValidationCode(string json)
    {
        var response = await _client.PostAsync(
            "/test/errors/body", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var body = await ReadJson(response);
        Assert.Equal("VALIDATION", body.RootElement.GetProperty("code").GetString());
    }

    private static async Task<JsonDocument> ReadJson(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());
}
