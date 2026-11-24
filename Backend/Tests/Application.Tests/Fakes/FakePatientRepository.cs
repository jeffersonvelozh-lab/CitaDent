using Application.Patients;
using Domain.Patients;

namespace Application.Tests.Fakes;

/// <summary>In-memory patients. Like the unique index, a document is unique per type.</summary>
internal sealed class FakePatientRepository : IPatientRepository
{
    private readonly List<Patient> _patients = [];

    public IReadOnlyList<Patient> All => _patients;

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
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_patients.Any(p => p.DocumentType == documentType && p.DocumentNumber == documentNumber));

    public void Add(Patient patient) => _patients.Add(patient);
}
