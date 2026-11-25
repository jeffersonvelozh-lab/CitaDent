using Application.ScheduleBlocks;
using Domain.Common;
using Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class ScheduleBlockRepository(CitaDentDbContext context) : IScheduleBlockRepository
{
    // Tracked, so the use case can remove it and save through the unit of work.
    public Task<ScheduleBlock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.ScheduleBlocks.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ScheduleBlock>> ListAsync(
        TimeRange range,
        Guid? dentistId,
        bool includeClinicWide,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(range);

        var start = range.Start;
        var end = range.End;

        // Half-open ranges: they overlap when each one starts before the other ends.
        var query = context.ScheduleBlocks.AsNoTracking()
            .Where(b => b.Range.Start < end && start < b.Range.End);

        query = (dentistId, includeClinicWide) switch
        {
            ({ } id, true) => query.Where(b => b.DentistId == id || b.DentistId == null),
            ({ } id, false) => query.Where(b => b.DentistId == id),
            (null, true) => query,
            (null, false) => query.Where(b => b.DentistId != null)
        };

        return await query
            .OrderBy(b => b.Range.Start)
            .ThenBy(b => b.Id)
            .ToListAsync(cancellationToken);
    }

    public void Add(ScheduleBlock block) => context.ScheduleBlocks.Add(block);

    public void Remove(ScheduleBlock block) => context.ScheduleBlocks.Remove(block);
}
