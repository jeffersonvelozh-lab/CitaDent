using Domain.Procedures;

namespace Application.Procedures;

/// <summary>A procedure as the use cases return it.</summary>
public sealed record ProcedureDto(
    Guid Id,
    string Name,
    string? Description,
    int DurationMinutes,
    decimal BasePrice,
    bool IsActive)
{
    public static ProcedureDto From(Procedure procedure) => new(
        procedure.Id,
        procedure.Name,
        procedure.Description,
        procedure.DurationMinutes,
        procedure.BasePrice,
        procedure.IsActive);
}
