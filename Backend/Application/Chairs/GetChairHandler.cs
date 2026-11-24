using Application.Common;

namespace Application.Chairs;

/// <summary>One chair by id, active or not.</summary>
public sealed class GetChairHandler(IChairRepository chairs) : IQueryHandler<GetChairQuery, ChairDto>
{
    public async Task<ChairDto> HandleAsync(GetChairQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var chair = await chairs.GetByIdAsync(query.Id, cancellationToken)
            ?? throw ChairErrors.NotFound(query.Id);

        return ChairDto.From(chair);
    }
}
