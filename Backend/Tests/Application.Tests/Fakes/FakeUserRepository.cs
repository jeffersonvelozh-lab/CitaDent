using Application.Users;
using Domain.Users;

namespace Application.Tests.Fakes;

/// <summary>In-memory users. Emails are stored normalized by the domain, so they compare exactly.</summary>
internal sealed class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users = [];

    public IReadOnlyList<User> All => _users;

    /// <summary>Puts a user in the store, as if it had been saved before the test.</summary>
    public User Seed(User user)
    {
        _users.Add(user);
        return user;
    }

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.SingleOrDefault(u => u.Email == email));

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.SingleOrDefault(u => u.Id == id));

    public Task<bool> EmailExistsAsync(string email, Guid? excludingId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Any(u => u.Email == email && u.Id != excludingId));

    public Task<IReadOnlyList<User>> ListAsync(
        bool includeInactive,
        UserRole? role,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<User>>(_users
            .Where(u => (includeInactive || u.IsActive) && (role is null || u.Role == role))
            .OrderBy(u => u.FullName, StringComparer.Ordinal)
            .ThenBy(u => u.Email, StringComparer.Ordinal)
            .ThenBy(u => u.Id)
            .ToList());

    public Task<bool> AnyOtherActiveAdminAsync(Guid excludingId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Any(u => u.Role == UserRole.Admin && u.IsActive && u.Id != excludingId));

    public Task<bool> AnyWithRoleAsync(UserRole role, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Any(u => u.Role == role));

    public void Add(User user) => _users.Add(user);
}
