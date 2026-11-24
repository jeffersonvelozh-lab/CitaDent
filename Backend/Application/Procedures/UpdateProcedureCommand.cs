namespace Application.Procedures;

public sealed record UpdateProcedureCommand(
    Guid Id,
    string Name,
    string? Description,
    int DurationMinutes,
    decimal BasePrice);
