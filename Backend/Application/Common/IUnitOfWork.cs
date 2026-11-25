namespace Application.Common;

/// <summary>
/// Port that saves, in one transaction, every change the repositories collected during a use case.
/// A handler calls it once, at the end.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Saves the pending changes and returns how many rows were written. When the database rejects an appointment
    /// that overlaps another one of the same dentist or chair, it throws
    /// <see cref="Appointments.ScheduleConflictException"/> and nothing is saved.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
