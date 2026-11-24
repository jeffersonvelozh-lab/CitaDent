using Application.Patients;
using Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class PatientRepository(CitaDentDbContext context) : IPatientRepository
{
    // Tracked, so a use case can change it and save through the unit of work.
    public Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Patients.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    // Same columns as the unique index (document_type, document_number).
    public Task<bool> DocumentExistsAsync(
        DocumentType documentType,
        string documentNumber,
        CancellationToken cancellationToken = default) =>
        context.Patients.AnyAsync(
            p => p.DocumentType == documentType && p.DocumentNumber == documentNumber,
            cancellationToken);

    public void Add(Patient patient) => context.Patients.Add(patient);
}
