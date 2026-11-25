using System.Security.Claims;
using Domain.Users;

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

    /// <summary>
    /// The role of the signed-in user, from the token's role claim: the same one the authorization policies read.
    /// Every token this API issues carries a defined role, so anything else is a bug.
    /// </summary>
    public static UserRole GetUserRole(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var identity = user.Identity as ClaimsIdentity;
        var value = identity?.FindFirst(identity.RoleClaimType)?.Value;

        // By name only: Enum.TryParse would also take "1".
        foreach (var role in Enum.GetValues<UserRole>())
        {
            if (role.ToString() == value)
            {
                return role;
            }
        }

        throw new InvalidOperationException("The token has no valid role.");
    }
}
