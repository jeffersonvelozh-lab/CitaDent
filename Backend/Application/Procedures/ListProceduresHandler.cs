using Application.Common;

namespace Application.Procedures;

/// <summary>The procedures ordered by name.</summary>
public sealed class ListProceduresHandler(IProcedureRepository procedures)
    : IQueryHandler<ListProceduresQuery, IReadOnlyList<ProcedureDto>>
{
    public async Task<IReadOnlyList<ProcedureDto>> HandleAsync(
        ListProceduresQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var list = await procedures.ListAsync(query.IncludeInactive, cancellationToken);

        return list.Select(ProcedureDto.From).ToList();
    }
}
