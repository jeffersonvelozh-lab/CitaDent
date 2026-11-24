using Application.Common;

namespace Application.Dentists;

/// <summary>The dentists with their schedules, ordered by last name and first name.</summary>
public sealed class ListDentistsHandler(IDentistRepository dentists)
    : IQueryHandler<ListDentistsQuery, IReadOnlyList<DentistDto>>
{
    public async Task<IReadOnlyList<DentistDto>> HandleAsync(
        ListDentistsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var list = await dentists.ListAsync(query.IncludeInactive, cancellationToken);

        return list.Select(DentistDto.From).ToList();
    }
}
