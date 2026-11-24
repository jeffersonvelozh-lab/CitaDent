using Microsoft.AspNetCore.Mvc;

namespace WebApiRest.Errors;

public static class ErrorHandling
{
    /// <summary>Every error answers with ProblemDetails and a "code" the frontend can translate.</summary>
    public static IServiceCollection AddCitaDentErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();

        // Order matters: the first handler that accepts the exception answers.
        services.AddExceptionHandler<DomainExceptionHandler>();
        services.AddExceptionHandler<UnhandledExceptionHandler>();

        // The automatic 400 of [ApiController] (malformed JSON, wrong types) gets the same code as ours.
        services.Configure<ApiBehaviorOptions>(options =>
        {
            var defaultFactory = options.InvalidModelStateResponseFactory;

            options.InvalidModelStateResponseFactory = context =>
            {
                var result = defaultFactory(context);

                if (result is ObjectResult { Value: ProblemDetails problem })
                {
                    problem.Extensions["code"] = ErrorStatusCodes.ValidationCode;
                }

                return result;
            };
        });

        return services;
    }
}
