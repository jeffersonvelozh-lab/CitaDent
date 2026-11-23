using Domain.Users;

namespace Application.Users;

public interface IUserRepository
{
    /// <summary>
    /// Finds a user by email. The email must already be normalized (trimmed, lower case),
    /// the same way the domain stores it.
    /// </summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
}
