using Application.Common;
using Domain.Patients;

namespace Application.Patients;

public interface IPatientRepository
{
    /// <summary>Finds a patient by id. Changes to the returned entity are saved by IUnitOfWork.</summary>
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when a patient other than <paramref name="excludingId"/> already has this document (RN-10).
    /// The number must already be normalized by the domain (trimmed; passports in upper case).
    /// A cédula and a passport with the same digits do not clash.
    /// </summary>
    Task<bool> DocumentExistsAsync(
        DocumentType documentType,
        string documentNumber,
        Guid? excludingId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Patients where every word appears, ignoring case, in the first name, the last name or the
    /// document number. No words means every patient. Ordered by last name, first name and id, so
    /// pages do not overlap. The words are plain text: '%' and '_' are not wildcards.
    /// The patients are not tracked.
    /// </summary>
    Task<PagedResult<Patient>> SearchAsync(
        IReadOnlyList<string> words,
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Marks the patient to be inserted on the next IUnitOfWork.SaveChangesAsync.</summary>
    void Add(Patient patient);
}
