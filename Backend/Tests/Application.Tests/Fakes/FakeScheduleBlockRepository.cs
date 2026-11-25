using Application.ScheduleBlocks;
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

    public void Add(ScheduleBlock block) => _blocks.Add(block);

    public void Remove(ScheduleBlock block) => _blocks.Remove(block);
}
