namespace Application.Procedures;

public sealed record CreateProcedureCommand(string Name, string? Description, int DurationMinutes, decimal BasePrice);
