using Domain.Common;
using Domain.Scheduling;

namespace Application.ScheduleBlocks;

public interface IScheduleBlockRepository
{
    /// <summary>Finds a block by id. It is tracked, so it can be passed to <see cref="Remove"/>.</summary>
    Task<ScheduleBlock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The blocks that overlap <paramref name="range"/> (half-open, so one that ends at its start is left out),
    /// ordered by start and id, not tracked. With <paramref name="dentistId"/>, only that dentist's blocks;
    /// without it, every dentist's. <paramref name="includeClinicWide"/> adds the blocks of the whole clinic.
    /// </summary>
    Task<IReadOnlyList<ScheduleBlock>> ListAsync(
        TimeRange range,
        Guid? dentistId,
        bool includeClinicWide,
        CancellationToken cancellationToken = default);

    /// <summary>Marks the block to be inserted on the next IUnitOfWork.SaveChangesAsync.</summary>
    void Add(ScheduleBlock block);

    /// <summary>Marks the block to be deleted on the next IUnitOfWork.SaveChangesAsync.</summary>
    void Remove(ScheduleBlock block);
}
