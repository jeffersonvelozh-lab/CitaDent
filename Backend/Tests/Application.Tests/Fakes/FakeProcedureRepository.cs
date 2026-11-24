using Application.Procedures;
using Domain.Procedures;

namespace Application.Tests.Fakes;

/// <summary>In-memory procedures. Like the real one, names compare ignoring case.</summary>
internal sealed class FakeProcedureRepository : IProcedureRepository
{
    private readonly List<Procedure> _procedures = [];

    public IReadOnlyList<Procedure> All => _procedures;

    /// <summary>Puts a procedure in the store, as if it had been saved before the test.</summary>
    public Procedure Seed(Procedure procedure)
    {
        _procedures.Add(procedure);
        return procedure;
    }

    public Task<Procedure?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_procedures.SingleOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<Procedure>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Procedure>>(_procedures
            .Where(p => includeInactive || p.IsActive)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList());

    public Task<bool> NameExistsAsync(string name, Guid? excludingId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_procedures.Any(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) && p.Id != excludingId));

    public void Add(Procedure procedure) => _procedures.Add(procedure);
}
