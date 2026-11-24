using Application.Procedures;
using Domain.Procedures;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class ProcedureRepository(CitaDentDbContext context) : IProcedureRepository
{
    // Tracked, so the use case can change it and save through the unit of work.
    public Task<Procedure?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Procedures.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Procedure>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var query = context.Procedures.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        return await query.OrderBy(p => p.Name).ToListAsync(cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, Guid? excludingId, CancellationToken cancellationToken = default)
    {
        // lower() on both sides in SQL, so "Limpieza" and "limpieza" count as the same name.
        var lowered = name.ToLower();

        return context.Procedures.AnyAsync(
            p => p.Name.ToLower() == lowered && (excludingId == null || p.Id != excludingId),
            cancellationToken);
    }

    public void Add(Procedure procedure) => context.Procedures.Add(procedure);
}
