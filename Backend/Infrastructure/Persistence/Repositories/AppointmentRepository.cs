using Application.Appointments;
using Domain.Appointments;
using Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class AppointmentRepository(CitaDentDbContext context) : IAppointmentRepository
{
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
}
