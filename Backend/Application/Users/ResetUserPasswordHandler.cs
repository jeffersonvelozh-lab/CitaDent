using Application.Common;
using Application.Security;

namespace Application.Users;

/// <summary>
/// An admin sets a new password for a user of any role, without the old one (at least 12 characters).
/// The old password stops working at once; tokens already issued keep working until they expire.
/// </summary>
public sealed class ResetUserPasswordHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork) : ICommandHandler<ResetUserPasswordCommand, UserDto>
{
    public async Task<UserDto> HandleAsync(ResetUserPasswordCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        UserErrors.EnsurePasswordIsLongEnough(command.Password);

        var user = await users.GetByIdAsync(command.Id, cancellationToken)
            ?? throw UserErrors.NotFound(command.Id);

        user.ChangePasswordHash(passwordHasher.Hash(command.Password));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UserDto.From(user);
    }
}
