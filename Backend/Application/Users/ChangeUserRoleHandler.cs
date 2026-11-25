using Application.Common;
using Domain.Users;

namespace Application.Users;

/// <summary>
/// Moves a user between Admin and Reception (the domain refuses the Dentist role, VALIDATION). Taking the
/// admin role away from the last active admin is refused (LAST-ADMIN).
/// </summary>
public sealed class ChangeUserRoleHandler(IUserRepository users, IUnitOfWork unitOfWork)
    : ICommandHandler<ChangeUserRoleCommand, UserDto>
{
    public async Task<UserDto> HandleAsync(ChangeUserRoleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.GetByIdAsync(command.Id, cancellationToken)
            ?? throw UserErrors.NotFound(command.Id);

        var losesAdmin = user.Role == UserRole.Admin && command.Role != UserRole.Admin;

        if (losesAdmin && user.IsActive && !await users.AnyOtherActiveAdminAsync(user.Id, cancellationToken))
        {
            throw UserErrors.LastActiveAdmin();
        }

        user.ChangeRole(command.Role);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UserDto.From(user);
    }
}
