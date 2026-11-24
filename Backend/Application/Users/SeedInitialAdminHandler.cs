using Application.Security;
using Domain.Common;
using Domain.Users;

namespace Application.Users;

/// <summary>
/// Creates the first administrator so a new installation can be used. It is safe to run on every start:
/// once any administrator exists, it does nothing.
/// </summary>
public sealed class SeedInitialAdminHandler(IUserRepository users, IPasswordHasher passwordHasher)
{
    public const int MinimumPasswordLength = 12;

    private const string ValidationCode = "VALIDATION";

    public async Task<SeedInitialAdminResult> HandleAsync(
        SeedInitialAdminCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Checked before touching the database, so a weak password always stops the start.
        if (string.IsNullOrEmpty(command.Password) || command.Password.Length < MinimumPasswordLength)
        {
            throw new DomainException(
                ValidationCode,
                $"The initial admin password must have at least {MinimumPasswordLength} characters.");
        }

        if (await users.AnyWithRoleAsync(UserRole.Admin, cancellationToken))
        {
            return SeedInitialAdminResult.AdminAlreadyExists;
        }

        // Validates and normalizes the email and name before looking for duplicates.
        var admin = User.Create(
            command.Email, command.FullName, passwordHasher.Hash(command.Password), UserRole.Admin);

        if (await users.FindByEmailAsync(admin.Email, cancellationToken) is not null)
        {
            return SeedInitialAdminResult.EmailAlreadyInUse;
        }

        await users.AddAsync(admin, cancellationToken);

        return SeedInitialAdminResult.Created;
    }
}
