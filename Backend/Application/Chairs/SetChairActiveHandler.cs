using Application.Common;

namespace Application.Chairs;

/// <summary>
/// Activates or deactivates a chair. Chairs are never deleted, because appointments keep their id.
/// Asking for the state it already has changes nothing.
/// </summary>
public sealed class SetChairActiveHandler(IChairRepository chairs, IUnitOfWork unitOfWork)
    : ICommandHandler<SetChairActiveCommand, ChairDto>
{
    public async Task<ChairDto> HandleAsync(SetChairActiveCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var chair = await chairs.GetByIdAsync(command.Id, cancellationToken)
            ?? throw ChairErrors.NotFound(command.Id);

        if (command.IsActive)
        {
            chair.Activate();
        }
        else
        {
            chair.Deactivate();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ChairDto.From(chair);
    }
}
