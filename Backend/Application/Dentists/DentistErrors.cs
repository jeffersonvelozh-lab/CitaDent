using Application.Common;
using Domain.Common;

namespace Application.Dentists;

internal static class DentistErrors
{
    public static DomainException NotFound(Guid id) =>
        new(ApplicationErrorCodes.NotFound, $"Dentist {id} was not found.");

    // The email is personal data, so it is not repeated in the message (messages reach logs).
    public static DomainException DuplicateEmail() =>
        new(ApplicationErrorCodes.Duplicate, "A user with that email already exists.");

    public static DomainException DuplicateLicenseNumber() =>
        new(ApplicationErrorCodes.Duplicate, "A dentist with that license number already exists.");
}
