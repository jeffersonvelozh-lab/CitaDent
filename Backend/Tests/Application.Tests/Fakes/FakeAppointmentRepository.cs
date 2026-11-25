using Application.Appointments;
using Domain.Appointments;
using Domain.Common;

namespace Application.Tests.Fakes;

/// <summary>
/// In-memory appointments. ListScheduledOverlappingAsync returns <see cref="Overlapping"/> as given, without
/// filtering, and remembers what it was asked: the real query (status, dentist or chair, overlap) is tested
/// against PostgreSQL.
/// </summary>
internal sealed class FakeAppointmentRepository : IAppointmentRepository
{
    private readonly List<Appointment> _added = [];

    /// <summary>The appointments added by the use case.</summary>
    public IReadOnlyList<Appointment> Added => _added;

    public List<Appointment> Overlapping { get; } = [];

    public (TimeRange Slot, Guid DentistId, Guid ChairId)? LastQuery { get; private set; }

    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_added.Concat(Overlapping).FirstOrDefault(a => a.Id == id));

    public Task<IReadOnlyList<Appointment>> ListScheduledOverlappingAsync(
        TimeRange slot,
        Guid dentistId,
        Guid chairId,
        CancellationToken cancellationToken = default)
    {
        LastQuery = (slot, dentistId, chairId);
        return Task.FromResult<IReadOnlyList<Appointment>>(Overlapping.ToList());
    }

    public void Add(Appointment appointment) => _added.Add(appointment);
}
