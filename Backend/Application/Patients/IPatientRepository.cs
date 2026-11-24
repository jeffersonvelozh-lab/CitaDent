using Domain.Patients;

namespace Application.Patients;

public interface IPatientRepository
{
    /// <summary>Finds a patient by id. Changes to the returned entity are saved by IUnitOfWork.</summary>
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when a patient already has this document (RN-10). The number must already be normalized
    /// by the domain (trimmed; passports in upper case). A cédula and a passport with the same digits do not clash.
    /// </summary>
    Task<bool> DocumentExistsAsync(
        DocumentType documentType,
        string documentNumber,
        CancellationToken cancellationToken = default);

    /// <summary>Marks the patient to be inserted on the next IUnitOfWork.SaveChangesAsync.</summary>
    void Add(Patient patient);
}
