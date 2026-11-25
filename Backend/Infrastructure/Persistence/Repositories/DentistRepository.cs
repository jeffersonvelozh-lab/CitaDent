using Application.Dentists;
using Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

// The weekly schedule is an owned collection, so EF loads it with the dentist without an Include.
public sealed class DentistRepository(CitaDentDbContext context) : IDentistRepository
{
    // Tracked, so the use case can change it (schedule included) and save through the unit of work.
    public Task<Dentist?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Dentists.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Dentist>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var query = context.Dentists.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(d => d.IsActive);
        }

        return await query
            .OrderBy(d => d.LastName)
            .ThenBy(d => d.FirstName)
            .ThenBy(d => d.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> LicenseNumberExistsAsync(
        string licenseNumber,
        Guid? excludingId,
        CancellationToken cancellationToken = default)
    {
        // lower() on both sides in SQL, so "lic-123" and "LIC-123" count as the same license.
        var lowered = licenseNumber.ToLower();

        return context.Dentists.AnyAsync(
            d => d.LicenseNumber.ToLower() == lowered && (excludingId == null || d.Id != excludingId),
            cancellationToken);
    }

    public void Add(Dentist dentist) => context.Dentists.Add(dentist);
}
