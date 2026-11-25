using Application.Users;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class UserRepository(CitaDentDbContext context) : IUserRepository
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    // Tracked, so the use case can change it and save through the unit of work.
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<bool> AnyWithRoleAsync(UserRole role, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(u => u.Role == role, cancellationToken);

    public void Add(User user) => context.Users.Add(user);
}
