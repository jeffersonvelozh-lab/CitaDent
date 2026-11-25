using Application.Common;
using Application.Users;

namespace Application.Dentists;

/// <summary>
/// Activates or deactivates a dentist together with their user account, saved at once: an inactive dentist
/// cannot sign in. Dentists are never deleted, because appointments keep their id. Asking for the state it
/// already has changes nothing. Appointments already booked are not touched.
/// </summary>
public sealed class SetDentistActiveHandler(IDentistRepository dentists, IUserRepository users, IUnitOfWork unitOfWork)
    : ICommandHandler<SetDentistActiveCommand, DentistDto>
{
    public async Task<DentistDto> HandleAsync(SetDentistActiveCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var dentist = await dentists.GetByIdAsync(command.Id, cancellationToken)
            ?? throw DentistErrors.NotFound(command.Id);

        // The foreign key guarantees the user; a missing one is broken data, not a caller's mistake.
        var user = await users.GetByIdAsync(dentist.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"Dentist {dentist.Id} has no user {dentist.UserId}.");

        if (command.IsActive)
        {
            dentist.Activate();
            user.Activate();
        }
        else
        {
            dentist.Deactivate();
            user.Deactivate();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return DentistDto.From(dentist);
    }
}
