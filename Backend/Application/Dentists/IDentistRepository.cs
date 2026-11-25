using Domain.Scheduling;

namespace Application.Dentists;

public interface IDentistRepository
{
    /// <summary>
    /// Finds a dentist by id, with the weekly schedule. Changes to the returned entity (the schedule
    /// included) are saved by IUnitOfWork.
    /// </summary>
    Task<Dentist?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The dentists with their schedules, ordered by last name, first name and id; inactive ones only when
    /// <paramref name="includeInactive"/>. They are not tracked.
    /// </summary>
    Task<IReadOnlyList<Dentist>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when a dentist other than <paramref name="excludingId"/> already has this license number, ignoring
    /// case. It must already be trimmed. Pass null when registering, the dentist's own id when editing.
    /// </summary>
    Task<bool> LicenseNumberExistsAsync(
        string licenseNumber,
        Guid? excludingId,
        CancellationToken cancellationToken = default);

    /// <summary>Marks the dentist to be inserted on the next IUnitOfWork.SaveChangesAsync.</summary>
    void Add(Dentist dentist);
}
