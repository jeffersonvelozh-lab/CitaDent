using Application.Users;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class UserRepository(CitaDentDbContext context) : IUserRepository
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<bool> AnyWithRoleAsync(UserRole role, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(u => u.Role == role, cancellationToken);

    public void Add(User user) => context.Users.Add(user);
}
