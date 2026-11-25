namespace WebApiRest.Contracts.Appointments;

/// <summary>Body of POST /api/appointments/{id}/cancel: <c>{ "reason": "El paciente viajó" }</c>. The reason is required (RN-08).</summary>
public sealed record CancelAppointmentRequest(string? Reason);
