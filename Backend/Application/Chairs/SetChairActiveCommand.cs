namespace Application.Chairs;

/// <summary>Activates (<paramref name="IsActive"/> true) or deactivates a chair.</summary>
public sealed record SetChairActiveCommand(Guid Id, bool IsActive);
