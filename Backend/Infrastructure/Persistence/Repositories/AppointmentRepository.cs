using Application.Appointments;
using Domain.Appointments;
using Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class AppointmentRepository(CitaDentDbContext context) : IAppointmentRepository
{
    // Tracked, so the use case can change it and save through the unit of work.
    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Appointments.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public void Add(Appointment appointment) => context.Appointments.Add(appointment);

    public async Task<IReadOnlyList<Appointment>> ListScheduledOverlappingAsync(
        TimeRange slot,
        Guid dentistId,
        Guid chairId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slot);

        var start = slot.Start;
        var end = slot.End;

        // Half-open ranges: they overlap when each one starts before the other ends.
        return await context.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Scheduled
                && (a.DentistId == dentistId || a.ChairId == chairId)
                && a.Slot.Start < end
                && start < a.Slot.End)
            .OrderBy(a => a.Slot.Start)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AgendaItemDto>> ListAgendaAsync(
        TimeRange range,
        Guid? dentistId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(range);

        var start = range.Start;
        var end = range.End;

        var inRange = context.Appointments.AsNoTracking()
            .Where(a => a.Slot.Start < end && start < a.Slot.End);

        if (dentistId is { } id)
        {
            inRange = inRange.Where(a => a.DentistId == id);
        }

        // One query with inner joins: every appointment references existing rows (foreign keys), so none is lost.
        var query =
            from a in inRange
            join patient in context.Patients on a.PatientId equals patient.Id
            join dentist in context.Dentists on a.DentistId equals dentist.Id
            join chair in context.Chairs on a.ChairId equals chair.Id
            join procedure in context.Procedures on a.ProcedureId equals procedure.Id
            orderby a.Slot.Start, a.Id
            select new AgendaItemDto(
                a.Id,
                a.Slot.Start,
                a.Slot.End,
                a.Status,
                a.CancelReason,
                patient.Id,
                patient.FirstName + " " + patient.LastName,
                dentist.Id,
                dentist.FirstName + " " + dentist.LastName,
                chair.Id,
                chair.Name,
                procedure.Id,
                procedure.Name);

        return await query.ToListAsync(cancellationToken);
    }
}
