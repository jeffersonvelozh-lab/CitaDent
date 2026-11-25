using Application.Appointments;
using Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    private static readonly Type[] HandlerInterfaces = [typeof(ICommandHandler<,>), typeof(IQueryHandler<,>)];

    /// <summary>
    /// Registers every use case of this assembly: each non-abstract class that implements
    /// <see cref="ICommandHandler{TCommand, TResult}"/> or <see cref="IQueryHandler{TQuery, TResult}"/>,
    /// as itself and as that interface, scoped to the request.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        foreach (var handler in FindHandlerTypes())
        {
            services.AddScoped(handler);

            foreach (var contract in handler.GetInterfaces().Where(IsHandlerInterface))
            {
                services.AddScoped(contract, provider => provider.GetRequiredService(handler));
            }
        }

        // Helpers shared by use cases, which are not handlers themselves.
        services.AddScoped<SchedulingContextLoader>();

        return services;
    }

    /// <summary>Every use case handler in Application. Public so a test can check they all resolve.</summary>
    public static IReadOnlyList<Type> FindHandlerTypes() =>
        typeof(DependencyInjection).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && type.GetInterfaces().Any(IsHandlerInterface))
            .ToList();

    /// <summary>True for a closed ICommandHandler&lt;,&gt; or IQueryHandler&lt;,&gt;.</summary>
    public static bool IsHandlerInterface(Type type) =>
        type.IsGenericType && HandlerInterfaces.Contains(type.GetGenericTypeDefinition());
}
