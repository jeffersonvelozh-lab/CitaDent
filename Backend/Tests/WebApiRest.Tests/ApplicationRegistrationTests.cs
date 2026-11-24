using Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace WebApiRest.Tests;

/// <summary>Every use case of Application can be resolved from the API container, so none is left unregistered.</summary>
public class ApplicationRegistrationTests(CitaDentApiFactory factory) : IClassFixture<CitaDentApiFactory>
{
    public static TheoryData<Type> HandlerTypes() => new(Application.DependencyInjection.FindHandlerTypes());

    [Fact]
    public void FindHandlerTypes_FindsTheExistingUseCases()
    {
        var names = Application.DependencyInjection.FindHandlerTypes().Select(t => t.Name).ToList();

        Assert.Contains("LoginHandler", names);
        Assert.Contains("SeedInitialAdminHandler", names);
        Assert.Contains("CreateProcedureHandler", names);
        Assert.Contains("ListProceduresHandler", names); // A query handler.
    }

    [Theory]
    [MemberData(nameof(HandlerTypes))]
    public void EveryHandler_ResolvesAsItselfAndAsItsInterface(Type handlerType)
    {
        using var scope = factory.Services.CreateScope();

        var byClass = scope.ServiceProvider.GetRequiredService(handlerType);
        var contract = handlerType.GetInterfaces().Single(Application.DependencyInjection.IsHandlerInterface);
        var byInterface = scope.ServiceProvider.GetRequiredService(contract);

        // Same instance within a request.
        Assert.Same(byClass, byInterface);
    }

    [Fact]
    public void UnitOfWork_IsTheSameDbContextTheRepositoriesUse()
    {
        using var scope = factory.Services.CreateScope();

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var context = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.CitaDentDbContext>();

        Assert.Same(context, unitOfWork);
    }
}
