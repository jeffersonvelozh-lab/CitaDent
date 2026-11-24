namespace Application.Procedures;

/// <summary>Activates (<paramref name="IsActive"/> true) or deactivates a procedure.</summary>
public sealed record SetProcedureActiveCommand(Guid Id, bool IsActive);
