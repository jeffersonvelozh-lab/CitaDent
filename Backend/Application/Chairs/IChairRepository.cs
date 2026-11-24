using Domain.Scheduling;

namespace Application.Chairs;

public interface IChairRepository
{
    /// <summary>Finds a chair by id. Changes to the returned entity are saved by IUnitOfWork.</summary>
    Task<Chair?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The chairs ordered by name; inactive ones only when <paramref name="includeInactive"/>.</summary>
    Task<IReadOnlyList<Chair>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when a chair other than <paramref name="excludingId"/> has this name, ignoring case.
    /// The name must already be trimmed.
    /// </summary>
    Task<bool> NameExistsAsync(string name, Guid? excludingId, CancellationToken cancellationToken = default);

    /// <summary>Marks the chair to be inserted on the next IUnitOfWork.SaveChangesAsync.</summary>
    void Add(Chair chair);
}
