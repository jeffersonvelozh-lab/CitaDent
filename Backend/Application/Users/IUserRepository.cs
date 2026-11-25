using Domain.Users;

namespace Application.Users;

public interface IUserRepository
{
    /// <summary>
    /// Finds a user by email. The email must already be normalized (trimmed, lower case),
    /// the same way the domain stores it.
    /// </summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a user by id. Changes to the returned entity are saved by IUnitOfWork.
    /// </summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True when at least one user, active or not, has the role.</summary>
    Task<bool> AnyWithRoleAsync(UserRole role, CancellationToken cancellationToken = default);

    /// <summary>Marks the user to be inserted; it is written when the use case calls IUnitOfWork.SaveChangesAsync.</summary>
    void Add(User user);
}
