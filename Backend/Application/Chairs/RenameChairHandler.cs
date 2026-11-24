using Application.Common;

namespace Application.Chairs;

/// <summary>
/// Changes a chair's name. Another chair may not have it, ignoring case; the chair's own name
/// in other letter case is fine.
/// </summary>
public sealed class RenameChairHandler(IChairRepository chairs, IUnitOfWork unitOfWork)
    : ICommandHandler<RenameChairCommand, ChairDto>
{
    public async Task<ChairDto> HandleAsync(RenameChairCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var chair = await chairs.GetByIdAsync(command.Id, cancellationToken)
            ?? throw ChairErrors.NotFound(command.Id);

        // Domain: validates the name (VALIDATION) and trims it. Nothing is saved if a later check
        // fails, because SaveChangesAsync is only called at the end.
        chair.Rename(command.Name);

        if (await chairs.NameExistsAsync(chair.Name, chair.Id, cancellationToken))
        {
            throw ChairErrors.DuplicateName(chair.Name);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ChairDto.From(chair);
    }
}
