namespace Application.Appointments;

/// <summary>
/// The appointments that touch the clinic days <paramref name="From"/> to <paramref name="To"/>, both included, in
/// every state. With <paramref name="DentistId"/>, only that dentist's; without it, every dentist's.
/// </summary>
public sealed record ListAppointmentsQuery(DateOnly From, DateOnly To, Guid? DentistId);
