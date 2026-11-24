using Application.Users;
using Domain.Common;

namespace WebApiRest.Startup;

/// <summary>
/// Creates the first administrator at startup from the "Seed:Admin" section (Email, FullName, Password).
/// The password is a secret: user-secrets in development, the Seed__Admin__Password variable elsewhere.
/// </summary>
public static class InitialAdminSeeding
{
    public const string SectionName = "Seed:Admin";

    public static async Task SeedInitialAdminAsync(this WebApplication app)
    {
        var section = app.Configuration.GetSection(SectionName);
        var email = section["Email"];
        var fullName = section["FullName"];
        var password = section["Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrEmpty(password))
        {
            app.Logger.LogWarning(
                "Initial admin not seeded: {Section}:Email, FullName and Password must all be configured.",
                SectionName);
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<SeedInitialAdminHandler>();

        SeedInitialAdminResult result;
        try
        {
            result = await handler.HandleAsync(new SeedInitialAdminCommand(email, fullName, password));
        }
        catch (DomainException ex)
        {
            // A bad seed configuration stops the start, like a missing JWT key.
            throw new InvalidOperationException($"Invalid {SectionName} configuration: {ex.Message}", ex);
        }

        switch (result)
        {
            case SeedInitialAdminResult.Created:
                app.Logger.LogInformation("Initial admin {Email} created.", email);
                break;
            case SeedInitialAdminResult.EmailAlreadyInUse:
                app.Logger.LogWarning(
                    "Initial admin not seeded: another user already has the email {Email}.", email);
                break;
            case SeedInitialAdminResult.AdminAlreadyExists:
                app.Logger.LogDebug("Initial admin not seeded: an administrator already exists.");
                break;
        }
    }
}
