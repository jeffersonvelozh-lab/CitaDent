using Application.Common;
using Domain.Users;

namespace Application.Users;

/// <summary>
/// Changes the display name of a reception or admin account (the domain trims it and rejects a blank or too
/// long one, VALIDATION). A dentist's name follows their dentist record, so it is changed through
/// UpdateDentistHandler and refused here (VALIDATION).
/// </summary>
public sealed class ChangeUserFullNameHandler(IUserRepository users, IUnitOfWork unitOfWork)
    : ICommandHandler<ChangeUserFullNameCommand, UserDto>
{
    public async Task<UserDto> HandleAsync(ChangeUserFullNameCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.GetByIdAsync(command.Id, cancellationToken)
            ?? throw UserErrors.NotFound(command.Id);

        if (user.Role == UserRole.Dentist)
        {
            throw UserErrors.DentistNameFollowsTheirRecord();
        }

        user.ChangeFullName(command.FullName);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UserDto.From(user);
    }
}
