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

    public Task<bool> AnyWithRoleAsync(UserRole role, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Any(u => u.Role == role));

    public void Add(User user) => _users.Add(user);
}
