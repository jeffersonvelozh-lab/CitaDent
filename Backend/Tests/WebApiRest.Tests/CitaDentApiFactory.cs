using Domain.Users;
using Infrastructure.Security;
using Infrastructure.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

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
