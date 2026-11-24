using Application.Chairs;
using Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class ChairRepository(CitaDentDbContext context) : IChairRepository
{
    // Tracked, so the use case can change it and save through the unit of work.
    public Task<Chair?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Chairs.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Chair>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var query = context.Chairs.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, Guid? excludingId, CancellationToken cancellationToken = default)
    {
        // lower() on both sides in SQL, so "Chair 1" and "CHAIR 1" count as the same name.
        var lowered = name.ToLower();

        return context.Chairs.AnyAsync(
            c => c.Name.ToLower() == lowered && (excludingId == null || c.Id != excludingId),
            cancellationToken);
    }

    public void Add(Chair chair) => context.Chairs.Add(chair);
}
