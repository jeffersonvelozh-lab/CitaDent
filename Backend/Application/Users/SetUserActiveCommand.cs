namespace Application.Users;

/// <summary>Activates (<paramref name="IsActive"/> true) or deactivates a user account.</summary>
public sealed record SetUserActiveCommand(Guid Id, bool IsActive);
