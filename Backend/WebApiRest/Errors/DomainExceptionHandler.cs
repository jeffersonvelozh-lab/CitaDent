using Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WebApiRest.Errors;

/// <summary>
/// Turns a <see cref="DomainException"/> into a ProblemDetails response with its code
/// (for example "RN-02"), which the frontend maps to a Spanish message.
/// </summary>
public sealed class DomainExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        var status = ErrorStatusCodes.For(domainException.Code);
        httpContext.Response.StatusCode = status;

        // Title, type and traceId are filled in by the ProblemDetails defaults.
        var problem = new ProblemDetails { Status = status, Detail = domainException.Message };
        problem.Extensions["code"] = domainException.Code;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });
    }
}
