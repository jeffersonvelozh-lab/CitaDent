using Application.Common;

namespace Application.Users;

/// <summary>
/// Changes the email a user signs in with, for any role. Another user may not have it (DUPLICATE); the
/// user's own email in other letter case is fine. The old email stops working at once.
/// </summary>
public sealed class ChangeUserEmailHandler(IUserRepository users, IUnitOfWork unitOfWork)
    : ICommandHandler<ChangeUserEmailCommand, UserDto>
{
    public async Task<UserDto> HandleAsync(ChangeUserEmailCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.GetByIdAsync(command.Id, cancellationToken)
            ?? throw UserErrors.NotFound(command.Id);

        // Domain: validates and normalizes (VALIDATION). Nothing is saved if a later check fails, because
        // SaveChangesAsync is only called at the end.
        user.ChangeEmail(command.Email);

        if (await users.EmailExistsAsync(user.Email, user.Id, cancellationToken))
        {
            throw UserErrors.DuplicateEmail();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UserDto.From(user);
    }
}
