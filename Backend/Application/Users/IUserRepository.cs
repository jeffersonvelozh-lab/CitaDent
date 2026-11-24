using Domain.Users;

namespace Application.Users;

public interface IUserRepository
{
    /// <summary>
    /// Finds a user by email. The email must already be normalized (trimmed, lower case),
    /// the same way the domain stores it.
    /// </summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>True when at least one user, active or not, has the role.</summary>
    Task<bool> AnyWithRoleAsync(UserRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the user and saves at once. Temporary: when the unit of work arrives (task 1b-07),
    /// saving moves there.
    /// </summary>
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
