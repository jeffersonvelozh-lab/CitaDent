using Domain.Appointments;
using Domain.Common;

namespace Application.Appointments;

public interface IAppointmentRepository
{
    /// <summary>Finds an appointment by id. Changes to the returned entity are saved by IUnitOfWork.</summary>
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The Scheduled appointments of the dentist or of the chair that overlap <paramref name="slot"/>, not
    /// tracked. The slot is half-open, so one that ends when it starts (RN-04) is left out, and so are
    /// cancelled, completed and no-show appointments, which no longer occupy time.
    /// </summary>
    Task<IReadOnlyList<Appointment>> ListScheduledOverlappingAsync(
        TimeRange slot,
        Guid dentistId,
        Guid chairId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The appointments that overlap <paramref name="range"/>, in every state, ordered by start and then by id,
    /// with the names the agenda shows. The range is half-open, so one that ends when it starts is left out.
    /// With <paramref name="dentistId"/>, only that dentist's.
    /// </summary>
    Task<IReadOnlyList<AgendaItemDto>> ListAgendaAsync(
        TimeRange range,
        Guid? dentistId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the appointment to be inserted on the next IUnitOfWork.SaveChangesAsync, which throws
    /// <see cref="ScheduleConflictException"/> if the database finds the dentist or the chair already taken.
    /// </summary>
    void Add(Appointment appointment);
}
