using Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers every use case of this assembly: each non-abstract class that implements
    /// <see cref="ICommandHandler{TCommand, TResult}"/>, as itself and as that interface, scoped to the request.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        foreach (var handler in FindHandlerTypes())
        {
            services.AddScoped(handler);

            foreach (var contract in handler.GetInterfaces().Where(IsCommandHandlerInterface))
            {
                services.AddScoped(contract, provider => provider.GetRequiredService(handler));
            }
        }

        return services;
    }

    /// <summary>Every use case handler in Application. Public so a test can check they all resolve.</summary>
    public static IReadOnlyList<Type> FindHandlerTypes() =>
        typeof(DependencyInjection).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && type.GetInterfaces().Any(IsCommandHandlerInterface))
            .ToList();

    private static bool IsCommandHandlerInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICommandHandler<,>);
}
