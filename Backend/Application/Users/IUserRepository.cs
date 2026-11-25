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

    /// <summary>
    /// True when a user other than <paramref name="excludingId"/> already has this email. It must already be
    /// normalized. Pass null when creating, the user's own id when changing it.
    /// </summary>
    Task<bool> EmailExistsAsync(string email, Guid? excludingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The users ordered by full name, email and id; inactive ones only when <paramref name="includeInactive"/>,
    /// and only those with <paramref name="role"/> when it is given. They are not tracked.
    /// </summary>
    Task<IReadOnlyList<User>> ListAsync(
        bool includeInactive,
        UserRole? role,
        CancellationToken cancellationToken = default);

    /// <summary>True when an active admin other than <paramref name="excludingId"/> exists.</summary>
    Task<bool> AnyOtherActiveAdminAsync(Guid excludingId, CancellationToken cancellationToken = default);

    /// <summary>True when at least one user, active or not, has the role.</summary>
    Task<bool> AnyWithRoleAsync(UserRole role, CancellationToken cancellationToken = default);

    /// <summary>Marks the user to be inserted; it is written when the use case calls IUnitOfWork.SaveChangesAsync.</summary>
    void Add(User user);
}
