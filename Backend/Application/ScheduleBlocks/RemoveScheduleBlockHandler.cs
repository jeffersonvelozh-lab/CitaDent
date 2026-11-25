using Application.Common;

namespace Application.ScheduleBlocks;

/// <summary>
/// Deletes a schedule block, past ones included: a block is not part of any patient's history, so it is
/// removed for good. Returns the block as it was.
/// </summary>
public sealed class RemoveScheduleBlockHandler(IScheduleBlockRepository blocks, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveScheduleBlockCommand, ScheduleBlockDto>
{
    public async Task<ScheduleBlockDto> HandleAsync(
        RemoveScheduleBlockCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var block = await blocks.GetByIdAsync(command.Id, cancellationToken)
            ?? throw ScheduleBlockErrors.NotFound(command.Id);

        blocks.Remove(block);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ScheduleBlockDto.From(block);
    }
}
