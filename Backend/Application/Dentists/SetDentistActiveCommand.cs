namespace Application.Dentists;

/// <summary>Activates (<paramref name="IsActive"/> true) or deactivates a dentist and their user account.</summary>
public sealed record SetDentistActiveCommand(Guid Id, bool IsActive);
