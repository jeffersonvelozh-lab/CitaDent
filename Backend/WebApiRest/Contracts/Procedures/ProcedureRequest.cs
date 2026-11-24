namespace WebApiRest.Contracts.Procedures;

/// <summary>Body of POST /api/procedures and PUT /api/procedures/{id}.</summary>
public sealed record ProcedureRequest(string? Name, string? Description, int DurationMinutes, decimal BasePrice);
