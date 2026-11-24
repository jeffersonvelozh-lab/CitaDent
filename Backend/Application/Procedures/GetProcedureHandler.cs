using Application.Common;

namespace Application.Procedures;

/// <summary>One procedure by id, active or not.</summary>
public sealed class GetProcedureHandler(IProcedureRepository procedures)
    : IQueryHandler<GetProcedureQuery, ProcedureDto>
{
    public async Task<ProcedureDto> HandleAsync(GetProcedureQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var procedure = await procedures.GetByIdAsync(query.Id, cancellationToken)
            ?? throw ProcedureErrors.NotFound(query.Id);

        return ProcedureDto.From(procedure);
    }
}
