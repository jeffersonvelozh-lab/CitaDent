using Domain.Procedures;

namespace Application.Procedures;

public interface IProcedureRepository
{
    /// <summary>Finds a procedure by id. Changes to the returned entity are saved by IUnitOfWork.</summary>
    Task<Procedure?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The procedures ordered by name; inactive ones only when <paramref name="includeInactive"/>.</summary>
    Task<IReadOnlyList<Procedure>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when a procedure other than <paramref name="excludingId"/> has this name, ignoring case.
    /// The name must already be trimmed.
    /// </summary>
    Task<bool> NameExistsAsync(string name, Guid? excludingId, CancellationToken cancellationToken = default);

    /// <summary>Marks the procedure to be inserted on the next IUnitOfWork.SaveChangesAsync.</summary>
    void Add(Procedure procedure);
}
