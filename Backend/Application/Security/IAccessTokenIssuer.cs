using Domain.Users;

namespace Application.Security;

/// <summary>Port for issuing a signed access token that identifies a user and carries its role.</summary>
public interface IAccessTokenIssuer
{
    AccessToken Issue(User user);
}
