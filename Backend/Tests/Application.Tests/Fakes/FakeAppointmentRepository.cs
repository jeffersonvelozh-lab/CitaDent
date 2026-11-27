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

    private readonly List<Appointment> _seeded = [];

    /// <summary>Puts an appointment in the store, as if it had been saved before the test.</summary>
    public Appointment Seed(Appointment appointment)
    {
        _seeded.Add(appointment);
        return appointment;
    }

    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_seeded.Concat(_added).Concat(Overlapping).FirstOrDefault(a => a.Id == id));

    public Task<IReadOnlyList<Appointment>> ListScheduledOverlappingAsync(
        TimeRange slot,
        Guid dentistId,
        Guid chairId,
        CancellationToken cancellationToken = default)
    {
        LastQuery = (slot, dentistId, chairId);
        return Task.FromResult<IReadOnlyList<Appointment>>(Overlapping.ToList());
    }

    /// <summary>The agenda rows ListAgendaAsync filters, as if the repository had joined the names.</summary>
    public List<AgendaItemDto> Agenda { get; } = [];

    /// <summary>The arguments of the last ListAgendaAsync call, to check how the use case turned days into instants.</summary>
    public (TimeRange Range, Guid? DentistId)? LastAgendaQuery { get; private set; }

    public Task<IReadOnlyList<AgendaItemDto>> ListAgendaAsync(
        TimeRange range,
        Guid? dentistId,
        CancellationToken cancellationToken = default)
    {
        LastAgendaQuery = (range, dentistId);

        return Task.FromResult<IReadOnlyList<AgendaItemDto>>(Agenda
            .Where(a => a.StartsAt < range.End && range.Start < a.EndsAt)
            .Where(a => dentistId is null || a.DentistId == dentistId)
            .OrderBy(a => a.StartsAt)
            .ThenBy(a => a.Id)
            .ToList());
    }

    public void Add(Appointment appointment) => _added.Add(appointment);
}
