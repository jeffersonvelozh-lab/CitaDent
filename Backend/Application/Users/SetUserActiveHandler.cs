using Application.Common;
using Domain.Users;

namespace Application.Users;

/// <summary>
/// Activates or deactivates a reception or admin account; an inactive user cannot sign in. Users are never
/// deleted. Deactivating the last active admin is refused (LAST-ADMIN). Dentist accounts are refused
/// (VALIDATION): they follow their dentist record, through SetDentistActiveHandler. Asking for the state it
/// already has changes nothing.
/// </summary>
public sealed class SetUserActiveHandler(IUserRepository users, IUnitOfWork unitOfWork)
    : ICommandHandler<SetUserActiveCommand, UserDto>
{
    public async Task<UserDto> HandleAsync(SetUserActiveCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.GetByIdAsync(command.Id, cancellationToken)
            ?? throw UserErrors.NotFound(command.Id);

        if (user.Role == UserRole.Dentist)
        {
            throw UserErrors.DentistAccountsFollowTheirRecord();
        }

        if (command.IsActive)
        {
            user.Activate();
        }
        else
        {
            if (user.Role == UserRole.Admin && user.IsActive
                && !await users.AnyOtherActiveAdminAsync(user.Id, cancellationToken))
            {
                throw UserErrors.LastActiveAdmin();
            }

            user.Deactivate();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UserDto.From(user);
    }
}
