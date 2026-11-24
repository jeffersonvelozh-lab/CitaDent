using System.Collections.Frozen;
using Application.Auth;

namespace WebApiRest.Errors;

/// <summary>
/// The one place that says which HTTP status each error code gets. Business rules not listed here
/// get 422, so a new rule never turns into a 500.
/// </summary>
public static class ErrorStatusCodes
{
    public const string ValidationCode = "VALIDATION";
    public const string InternalCode = "INTERNAL";

    public const int DefaultStatus = StatusCodes.Status422UnprocessableEntity;

    private static readonly FrozenDictionary<string, int> StatusByCode = new Dictionary<string, int>
    {
        // Malformed or missing data.
        [ValidationCode] = StatusCodes.Status400BadRequest,

        // Failed login.
        [LoginHandler.InvalidCredentialsCode] = StatusCodes.Status401Unauthorized,

        // Permission rules: who completes an appointment, who sees clinical notes, who voids a payment.
        ["RN-09"] = StatusCodes.Status403Forbidden,
        ["RN-15"] = StatusCodes.Status403Forbidden,
        ["RN-19"] = StatusCodes.Status403Forbidden,

        // The request clashes with what already exists: another appointment, a block or the current state.
        ["RN-02"] = StatusCodes.Status409Conflict,
        ["RN-03"] = StatusCodes.Status409Conflict,
        ["RN-05"] = StatusCodes.Status409Conflict,
        ["RN-07"] = StatusCodes.Status409Conflict,
        ["RN-11"] = StatusCodes.Status409Conflict,

        // Well-formed requests that break a business rule.
        ["RN-01"] = StatusCodes.Status422UnprocessableEntity,
        ["RN-06"] = StatusCodes.Status422UnprocessableEntity,
        ["RN-08"] = StatusCodes.Status422UnprocessableEntity,
        ["RN-10"] = StatusCodes.Status422UnprocessableEntity,
        ["RN-12"] = StatusCodes.Status422UnprocessableEntity,
        ["RN-13"] = StatusCodes.Status422UnprocessableEntity,
        ["RN-14"] = StatusCodes.Status422UnprocessableEntity,
        ["RN-16"] = StatusCodes.Status422UnprocessableEntity,
        ["RN-17"] = StatusCodes.Status422UnprocessableEntity,
        ["RN-18"] = StatusCodes.Status422UnprocessableEntity,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static int For(string code) =>
        StatusByCode.TryGetValue(code, out var status) ? status : DefaultStatus;
}
