namespace Application.Dentists;

/// <summary>
/// The dentist's complete new weekly schedule: it replaces the old one. An empty list leaves the
/// dentist without working hours, so nothing can be booked with them.
/// </summary>
public sealed record SetWeeklyScheduleCommand(Guid DentistId, IReadOnlyList<WorkingHoursDto> Hours);
