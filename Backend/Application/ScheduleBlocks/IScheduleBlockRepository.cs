using Domain.Scheduling;

namespace Application.ScheduleBlocks;

public interface IScheduleBlockRepository
{
    /// <summary>Finds a block by id. It is tracked, so it can be passed to <see cref="Remove"/>.</summary>
    Task<ScheduleBlock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Marks the block to be inserted on the next IUnitOfWork.SaveChangesAsync.</summary>
    void Add(ScheduleBlock block);

    /// <summary>Marks the block to be deleted on the next IUnitOfWork.SaveChangesAsync.</summary>
    void Remove(ScheduleBlock block);
}
