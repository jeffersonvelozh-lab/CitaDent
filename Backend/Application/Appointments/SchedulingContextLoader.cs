using Application.Common;
using Application.ScheduleBlocks;
using Domain.Appointments;
using Domain.Common;

namespace Application.Appointments;

/// <summary>
/// Builds the <see cref="SchedulingContext"/> the domain needs to decide whether a slot fits, loading only
/// what overlaps that slot: the blocks of the dentist and of the whole clinic (RN-05), and the Scheduled
/// appointments of the dentist or the chair (RN-02, RN-03). The domain only checks overlaps, so anything
/// outside the slot would change no decision. It does not check that the dentist or chair exist: the use
/// case loads them anyway.
/// </summary>
public sealed class SchedulingContextLoader(
    IScheduleBlockRepository blocks,
    IAppointmentRepository appointments,
    ClinicClock clock)
{
    public async Task<SchedulingContext> LoadAsync(
        Guid dentistId,
        Guid chairId,
        TimeRange slot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slot);

        // One after the other: both use the same DbContext, which does not allow parallel queries.
        var blocksInSlot = await blocks.ListAsync(slot, dentistId, includeClinicWide: true, cancellationToken);
        var appointmentsInSlot = await appointments.ListScheduledOverlappingAsync(slot, dentistId, chairId, cancellationToken);

        return new SchedulingContext(clock.TimeZone, blocksInSlot, appointmentsInSlot);
    }
}
