using Application.Common;
using Domain.Scheduling;

namespace Application.Chairs;

/// <summary>Adds a chair. Names are unique, ignoring case.</summary>
public sealed class CreateChairHandler(IChairRepository chairs, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateChairCommand, ChairDto>
{
    public async Task<ChairDto> HandleAsync(CreateChairCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Domain first: it validates the name (VALIDATION) and trims it.
        var chair = Chair.Create(command.Name);

        if (await chairs.NameExistsAsync(chair.Name, excludingId: null, cancellationToken))
        {
            throw ChairErrors.DuplicateName(chair.Name);
        }

        chairs.Add(chair);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ChairDto.From(chair);
    }
}
