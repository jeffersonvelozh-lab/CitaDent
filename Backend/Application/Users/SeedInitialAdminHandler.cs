using Application.Common;
using Application.Security;
using Domain.Common;
using Domain.Users;

namespace Application.Users;

/// <summary>
/// Creates the first administrator so a new installation can be used. It is safe to run on every start:
/// once any administrator exists, it does nothing. It is the reference example of the use case template
/// (see <see cref="ICommandHandler{TCommand, TResult}"/>).
/// </summary>
public sealed class SeedInitialAdminHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork) : ICommandHandler<SeedInitialAdminCommand, SeedInitialAdminResult>
{
    public const int MinimumPasswordLength = 12;

    public async Task<SeedInitialAdminResult> HandleAsync(
        SeedInitialAdminCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Input. Checked before touching the database, so a weak password always stops the start.
        if (string.IsNullOrEmpty(command.Password) || command.Password.Length < MinimumPasswordLength)
        {
            throw new DomainException(
                DomainException.ValidationCode,
                $"The initial admin password must have at least {MinimumPasswordLength} characters.");
        }

        // 2. Rules that need the database.
        if (await users.AnyWithRoleAsync(UserRole.Admin, cancellationToken))
        {
            return SeedInitialAdminResult.AdminAlreadyExists;
        }

        // 3. Domain: validates and normalizes the email and name before looking for duplicates.
        var admin = User.Create(
            command.Email, command.FullName, passwordHasher.Hash(command.Password), UserRole.Admin);

        if (await users.FindByEmailAsync(admin.Email, cancellationToken) is not null)
        {
            return SeedInitialAdminResult.EmailAlreadyInUse;
        }

        // 4. Save once, at the end.
        users.Add(admin);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return SeedInitialAdminResult.Created;
    }
}
