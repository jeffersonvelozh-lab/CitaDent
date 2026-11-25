using Domain.Appointments;
using Domain.Common;

namespace Application.Appointments;

public interface IAppointmentRepository
{
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
}
