using Application.ScheduleBlocks;
using Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class ScheduleBlockRepository(CitaDentDbContext context) : IScheduleBlockRepository
{
    // Tracked, so the use case can remove it and save through the unit of work.
    public Task<ScheduleBlock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.ScheduleBlocks.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

    public void Add(ScheduleBlock block) => context.ScheduleBlocks.Add(block);

    public void Remove(ScheduleBlock block) => context.ScheduleBlocks.Remove(block);
}
