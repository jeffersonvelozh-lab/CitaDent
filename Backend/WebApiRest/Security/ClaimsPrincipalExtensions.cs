using System.Security.Claims;

namespace WebApiRest.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The id of the signed-in user, from the token's "sub" claim (the name claim, see ConfigureJwtBearerOptions).
    /// Every token this API issues carries it, so a missing or malformed one is a bug, not a client error.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return Guid.TryParse(user.Identity?.Name, out var id) && id != Guid.Empty
            ? id
            : throw new InvalidOperationException("The token has no valid user id (sub claim).");
    }
}
