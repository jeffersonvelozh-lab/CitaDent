using Domain.Users;

namespace Application.Appointments;

/// <summary>Marks the appointment as done. <paramref name="ActorUserId"/> and <paramref name="ActorRole"/> are the signed-in user.</summary>
public sealed record CompleteAppointmentCommand(Guid Id, Guid ActorUserId, UserRole ActorRole);
