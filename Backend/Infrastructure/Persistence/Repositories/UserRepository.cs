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

    public Task<bool> EmailExistsAsync(string email, Guid? excludingId, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(u => u.Email == email && (excludingId == null || u.Id != excludingId), cancellationToken);

    public async Task<IReadOnlyList<User>> ListAsync(
        bool includeInactive,
        UserRole? role,
        CancellationToken cancellationToken = default)
    {
        var query = context.Users.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(u => u.IsActive);
        }

        if (role is not null)
        {
            query = query.Where(u => u.Role == role);
        }

        return await query
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.Email)
            .ThenBy(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> AnyOtherActiveAdminAsync(Guid excludingId, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(
            u => u.Role == UserRole.Admin && u.IsActive && u.Id != excludingId, cancellationToken);

    public Task<bool> AnyWithRoleAsync(UserRole role, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(u => u.Role == role, cancellationToken);

    public void Add(User user) => context.Users.Add(user);
}
