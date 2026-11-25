using Application.Common;

namespace Application.ScheduleBlocks;

/// <summary>One schedule block by id.</summary>
public sealed class GetScheduleBlockHandler(IScheduleBlockRepository blocks)
    : IQueryHandler<GetScheduleBlockQuery, ScheduleBlockDto>
{
    public async Task<ScheduleBlockDto> HandleAsync(GetScheduleBlockQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var block = await blocks.GetByIdAsync(query.Id, cancellationToken)
            ?? throw ScheduleBlockErrors.NotFound(query.Id);

        return ScheduleBlockDto.From(block);
    }
}
