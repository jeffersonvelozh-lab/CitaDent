using Application.ScheduleBlocks;
using Domain.Common;
using Domain.Scheduling;

namespace Application.Tests.Fakes;

/// <summary>In-memory schedule blocks.</summary>
internal sealed class FakeScheduleBlockRepository : IScheduleBlockRepository
{
    private readonly List<ScheduleBlock> _blocks = [];

    public IReadOnlyList<ScheduleBlock> All => _blocks;

    /// <summary>Puts a block in the store, as if it had been saved before the test.</summary>
    public ScheduleBlock Seed(ScheduleBlock block)
    {
        _blocks.Add(block);
        return block;
    }

    public Task<ScheduleBlock?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_blocks.SingleOrDefault(b => b.Id == id));

    /// <summary>The range of the last ListAsync call, to check how the use case turned days into instants.</summary>
    public TimeRange? LastListedRange { get; private set; }

    public Task<IReadOnlyList<ScheduleBlock>> ListAsync(
        TimeRange range,
        Guid? dentistId,
        bool includeClinicWide,
        CancellationToken cancellationToken = default)
    {
        LastListedRange = range;

        return Task.FromResult<IReadOnlyList<ScheduleBlock>>(_blocks
            .Where(b => b.Range.OverlapsWith(range))
            .Where(b => b.DentistId is null
                ? includeClinicWide
                : dentistId is null || b.DentistId == dentistId)
            .OrderBy(b => b.Range.Start)
            .ThenBy(b => b.Id)
            .ToList());
    }

    public void Add(ScheduleBlock block) => _blocks.Add(block);

    public void Remove(ScheduleBlock block) => _blocks.Remove(block);
}
