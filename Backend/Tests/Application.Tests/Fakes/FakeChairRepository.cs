using Application.Chairs;
using Domain.Scheduling;

namespace Application.Tests.Fakes;

/// <summary>In-memory chairs. Like the real one, names compare ignoring case.</summary>
internal sealed class FakeChairRepository : IChairRepository
{
    private readonly List<Chair> _chairs = [];

    public IReadOnlyList<Chair> All => _chairs;

    /// <summary>Puts a chair in the store, as if it had been saved before the test.</summary>
    public Chair Seed(Chair chair)
    {
        _chairs.Add(chair);
        return chair;
    }

    public Task<Chair?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_chairs.SingleOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Chair>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Chair>>(_chairs
            .Where(c => includeInactive || c.IsActive)
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .ToList());

    public Task<bool> NameExistsAsync(string name, Guid? excludingId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_chairs.Any(c =>
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase) && c.Id != excludingId));

    public void Add(Chair chair) => _chairs.Add(chair);
}
