using Domain.Scheduling;

namespace Domain.Appointments;

/// <summary>
/// The data the domain needs to decide whether a new appointment fits. The application layer loads it
/// (only the records near the requested slot, not whole tables) and the domain applies the rules.
/// </summary>
/// <param name="ClinicTimeZone">The clinic's local time zone, used to read the dentists' working hours.</param>
/// <param name="Blocks">Vacations and holidays that may overlap the slot, for the dentist or the whole clinic.</param>
/// <param name="ExistingAppointments">Appointments of the dentist or the chair that may overlap the slot.</param>
public sealed record SchedulingContext(
    TimeZoneInfo ClinicTimeZone,
    IReadOnlyCollection<ScheduleBlock> Blocks,
    IReadOnlyCollection<Appointment> ExistingAppointments);