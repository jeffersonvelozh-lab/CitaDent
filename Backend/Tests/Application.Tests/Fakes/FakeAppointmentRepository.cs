using Application.Appointments;
using Domain.Appointments;
using Domain.Common;

namespace Application.Tests.Fakes;

/// <summary>
/// Returns the appointments it was given, without filtering, and remembers what it was asked.
/// The real query (status, dentist or chair, overlap) is tested against PostgreSQL.
/// </summary>
internal sealed class FakeAppointmentRepository : IAppointmentRepository
{
    public List<Appointment> Overlapping { get; } = [];

    public (TimeRange Slot, Guid DentistId, Guid ChairId)? LastQuery { get; private set; }

    public Task<IReadOnlyList<Appointment>> ListScheduledOverlappingAsync(
        TimeRange slot,
        Guid dentistId,
        Guid chairId,
        CancellationToken cancellationToken = default)
    {
        LastQuery = (slot, dentistId, chairId);
        return Task.FromResult<IReadOnlyList<Appointment>>(Overlapping.ToList());
    }
}
