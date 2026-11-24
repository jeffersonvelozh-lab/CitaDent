using Application.Common;

namespace Application.Chairs;

/// <summary>The chairs ordered by name.</summary>
public sealed class ListChairsHandler(IChairRepository chairs)
    : IQueryHandler<ListChairsQuery, IReadOnlyList<ChairDto>>
{
    public async Task<IReadOnlyList<ChairDto>> HandleAsync(
        ListChairsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var list = await chairs.ListAsync(query.IncludeInactive, cancellationToken);

        return list.Select(ChairDto.From).ToList();
    }
}
