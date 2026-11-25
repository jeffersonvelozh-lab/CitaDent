namespace Application.Appointments;

public sealed record CancelAppointmentCommand(Guid Id, string Reason);
