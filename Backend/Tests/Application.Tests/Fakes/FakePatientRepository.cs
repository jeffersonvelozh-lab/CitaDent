using Application.Common;
using Application.Patients;
using Domain.Patients;

namespace Application.Tests.Fakes;

/// <summary>
/// In-memory patients. Like the unique index, a document is unique per type; like the real search,
/// every word must appear (ignoring case) in the first name, last name or document.
/// </summary>
internal sealed class FakePatientRepository : IPatientRepository
{
    private readonly List<Patient> _patients = [];

    public IReadOnlyList<Patient> All => _patients;

    /// <summary>The words of the last search, to check how the handler split the term.</summary>
    public IReadOnlyList<string>? LastSearchWords { get; private set; }

    /// <summary>Puts a patient in the store, as if it had been saved before the test.</summary>
    public Patient Seed(Patient patient)
    {
        _patients.Add(patient);
        return patient;
    }

    public Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_patients.SingleOrDefault(p => p.Id == id));

    public Task<bool> DocumentExistsAsync(
        DocumentType documentType,
        string documentNumber,
        Guid? excludingId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_patients.Any(p =>
            p.DocumentType == documentType && p.DocumentNumber == documentNumber && p.Id != excludingId));

    public Task<PagedResult<Patient>> SearchAsync(
        IReadOnlyList<string> words,
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        LastSearchWords = words;

        var matches = _patients
            .Where(p => includeInactive || p.IsActive)
            .Where(p => words.All(w =>
                p.FirstName.Contains(w, StringComparison.OrdinalIgnoreCase)
                || p.LastName.Contains(w, StringComparison.OrdinalIgnoreCase)
                || p.DocumentNumber.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p.LastName, StringComparer.Ordinal)
            .ThenBy(p => p.FirstName, StringComparer.Ordinal)
            .ThenBy(p => p.Id)
            .ToList();

        var items = matches.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult(new PagedResult<Patient>(items, page, pageSize, matches.Count));
    }

    public void Add(Patient patient) => _patients.Add(patient);
}
