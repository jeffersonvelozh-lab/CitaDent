using Application.Common;
using Domain.Common;

namespace Application.Users;

internal static class UserErrors
{
    public static DomainException NotFound(Guid id) =>
        new(ApplicationErrorCodes.NotFound, $"User {id} was not found.");

    // The email is personal data, so it is not repeated in the message (messages reach logs).
    public static DomainException DuplicateEmail() =>
        new(ApplicationErrorCodes.Duplicate, "A user with that email already exists.");

    public static DomainException LastActiveAdmin() =>
        new(ApplicationErrorCodes.LastActiveAdmin, "The clinic must keep at least one active administrator.");

    public static DomainException DentistAccountsFollowTheirRecord() =>
        new(DomainException.ValidationCode, "A dentist's account is activated or deactivated through the dentist.");

    public static DomainException DentistNameFollowsTheirRecord() =>
        new(DomainException.ValidationCode, "A dentist's name is changed through the dentist.");

    /// <summary>The same minimum as the initial administrator's password.</summary>
    public static void EnsurePasswordIsLongEnough(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < SeedInitialAdminHandler.MinimumPasswordLength)
        {
            throw new DomainException(
                DomainException.ValidationCode,
                $"The password must have at least {SeedInitialAdminHandler.MinimumPasswordLength} characters.");
        }
    }
}
