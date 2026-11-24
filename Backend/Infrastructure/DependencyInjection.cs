using Application.Common;
using Application.Procedures;
using Application.Security;
using Application.Users;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.TryAddSingleton(TimeProvider.System);

        services.AddDbContext<CitaDentDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        // The same DbContext instance of the request, so repositories and the unit of work share changes.
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<CitaDentDbContext>());

        // Stateless, so one instance serves the whole application.
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        // The app refuses to start when the JWT settings are missing or the signing key is too short.
        services.AddOptions<JwtOptions>()
            .Configure(options => JwtOptions.ReadFrom(configuration, options))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProcedureRepository, ProcedureRepository>();

        return services;
    }
}