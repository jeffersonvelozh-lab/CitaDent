using Application.Common;
using Application.Security;
using Domain.Common;
using Domain.Users;

namespace Application.Users;

/// <summary>
/// Creates a reception or admin account. The email is unique (DUPLICATE) and the password has at least
/// 12 characters. Dentist accounts are refused (VALIDATION): they are created with RegisterDentistHandler,
/// together with the dentist record.
/// </summary>
public sealed class CreateUserHandler(IUserRepository users, IPasswordHasher passwordHasher, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateUserCommand, UserDto>
{
    public async Task<UserDto> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Input.
        UserErrors.EnsurePasswordIsLongEnough(command.Password);

        if (command.Role == UserRole.Dentist)
        {
            throw new DomainException(
                DomainException.ValidationCode, "Dentist accounts are created together with the dentist.");
        }

        // 2. Domain: validates the role, email and name, and normalizes the email before looking for duplicates.
        var user = User.Create(command.Email, command.FullName, passwordHasher.Hash(command.Password), command.Role);

        // 3. Rules that need the database.
        if (await users.EmailExistsAsync(user.Email, excludingId: null, cancellationToken))
        {
            throw UserErrors.DuplicateEmail();
        }

        // 4. Save once, at the end.
        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UserDto.From(user);
    }
}
