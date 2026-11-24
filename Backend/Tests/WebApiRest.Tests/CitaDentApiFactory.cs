using System.Net.Http.Headers;
using Domain.Users;
using Infrastructure.Security;
using Infrastructure.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WebApiRest.Tests.Fakes;

namespace WebApiRest.Tests;

/// <summary>
/// Runs the real API in memory against a throwaway PostgreSQL database (one per test class).
/// The connection string and the JWT signing key are given as configuration, like in production.
/// </summary>
public sealed class CitaDentApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "api-tests-signing-key-with-at-least-32-bytes";
    public const string Issuer = "CitaDent";
    public const string Audience = "CitaDent.Api";

    public const string ActiveEmail = "recepcion@citadent.test";
    public const string InactiveEmail = "inactivo@citadent.test";
    public const string Password = "Clave-Segura-123";

    // The first admin, created by the API itself at startup from the Seed:Admin settings.
    public const string SeedAdminEmail = "admin@citadent.test";
    public const string SeedAdminPassword = "Clave-Admin-2026";

    private readonly PostgresFixture _database = new();

    public User ActiveUser { get; }

    public CitaDentApiFactory()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.Hash(Password);

        ActiveUser = User.Create(ActiveEmail, "Recepción de Prueba", hash, UserRole.Reception);

        var inactiveUser = User.Create(InactiveEmail, "Usuario Inactivo", hash, UserRole.Dentist);
        inactiveUser.Deactivate();

        _database.Save(ActiveUser, inactiveUser);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development, so neither user-secrets nor appsettings.Development.json are used.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _database.ConnectionString);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Seed:Admin:Email", SeedAdminEmail);
        builder.UseSetting("Seed:Admin:FullName", "Administración de Prueba");
        builder.UseSetting("Seed:Admin:Password", SeedAdminPassword);

        // Adds the test-only endpoints used by the authorization tests.
        builder.ConfigureTestServices(services => services
            .AddControllers()
            .AddApplicationPart(typeof(AuthorizationProbeController).Assembly));
    }

    /// <summary>A client that sends a valid token for a user with the given role.</summary>
    public HttpClient CreateClientWithToken(UserRole role) => CreateClientWithToken(IssueToken(role));

    public HttpClient CreateClientWithToken(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Issues a token the same way the API does. Change the key, the issuer or the clock to get tokens
    /// the API must reject. The user does not need to exist in the database: authorization reads only the token.
    /// </summary>
    public static string IssueToken(
        UserRole role,
        string signingKey = SigningKey,
        string issuer = Issuer,
        TimeProvider? clock = null)
    {
        var settings = new JwtOptions
        {
            Issuer = issuer,
            Audience = Audience,
            SigningKey = signingKey,
            ExpirationMinutes = 60
        };
        var user = User.Create($"{role.ToString().ToLowerInvariant()}@citadent.test", $"Test {role}", "hash", role);

        return new JwtAccessTokenIssuer(Options.Create(settings), clock ?? TimeProvider.System).Issue(user).Value;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _database.Dispose();
        }
    }
}
