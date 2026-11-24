using Application.Dentists;
using Domain.Scheduling;

namespace Application.Tests.Fakes;

/// <summary>In-memory dentists. Like the real one, license numbers compare ignoring case.</summary>
internal sealed class FakeDentistRepository : IDentistRepository
{
    private readonly List<Dentist> _dentists = [];

    public IReadOnlyList<Dentist> All => _dentists;

    /// <summary>Puts a dentist in the store, as if it had been saved before the test.</summary>
    public Dentist Seed(Dentist dentist)
    {
        _dentists.Add(dentist);
        return dentist;
    }

    public Task<Dentist?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_dentists.SingleOrDefault(d => d.Id == id));

    public Task<IReadOnlyList<Dentist>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Dentist>>(_dentists
            .Where(d => includeInactive || d.IsActive)
            .OrderBy(d => d.LastName, StringComparer.Ordinal)
            .ThenBy(d => d.FirstName, StringComparer.Ordinal)
            .ThenBy(d => d.Id)
            .ToList());

    public Task<bool> LicenseNumberExistsAsync(string licenseNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(_dentists.Any(d =>
            string.Equals(d.LicenseNumber, licenseNumber, StringComparison.OrdinalIgnoreCase)));

    public void Add(Dentist dentist) => _dentists.Add(dentist);
}
