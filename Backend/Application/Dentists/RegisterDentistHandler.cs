using Application.Common;
using Application.Security;
using Application.Users;
using Domain.Common;
using Domain.Scheduling;
using Domain.Users;

namespace Application.Dentists;

/// <summary>
/// Registers a dentist together with the user account they sign in with (role Dentist), saved at once:
/// there is never a dentist without a user, nor a dentist user without a dentist. The email and the
/// license number are unique. The weekly schedule starts empty and is set separately.
/// </summary>
public sealed class RegisterDentistHandler(
    IDentistRepository dentists,
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork) : ICommandHandler<RegisterDentistCommand, DentistDto>
{
    /// <summary>The same minimum as the initial administrator's password.</summary>
    public const int MinimumPasswordLength = SeedInitialAdminHandler.MinimumPasswordLength;

    public async Task<DentistDto> HandleAsync(RegisterDentistCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Input.
        if (string.IsNullOrEmpty(command.Password) || command.Password.Length < MinimumPasswordLength)
        {
            throw new DomainException(
                DomainException.ValidationCode,
                $"The password must have at least {MinimumPasswordLength} characters.");
        }

        // 2. Domain: validates and normalizes the email, names and license before looking for duplicates.
        var user = User.Create(
            command.Email, FullName(command.FirstName, command.LastName), passwordHasher.Hash(command.Password),
            UserRole.Dentist);

        var dentist = Dentist.Create(
            user.Id, command.FirstName, command.LastName, command.LicenseNumber, command.Specialty);

        // 3. Rules that need the database.
        if (await users.FindByEmailAsync(user.Email, cancellationToken) is not null)
        {
            throw DentistErrors.DuplicateEmail();
        }

        if (await dentists.LicenseNumberExistsAsync(dentist.LicenseNumber, cancellationToken))
        {
            throw DentistErrors.DuplicateLicenseNumber();
        }

        // 4. Save once, at the end: both rows or none.
        users.Add(user);
        dentists.Add(dentist);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return DentistDto.From(dentist);
    }

    // The user's display name. Two names of up to 80 characters can pass the user's 160, so it is cut;
    // blank names are left for Dentist.Create to reject.
    private static string FullName(string? firstName, string? lastName)
    {
        var fullName = $"{firstName?.Trim()} {lastName?.Trim()}".Trim();

        return fullName.Length > User.FullNameMaxLength ? fullName[..User.FullNameMaxLength] : fullName;
    }
}
