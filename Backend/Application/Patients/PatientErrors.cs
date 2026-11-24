using Application.Common;
using Domain.Common;

namespace Application.Patients;

internal static class PatientErrors
{
    public static DomainException NotFound(Guid id) =>
        new(ApplicationErrorCodes.NotFound, $"Patient {id} was not found.");

    // The document number is personal data, so it is not repeated in the message (messages reach logs).
    public static DomainException DuplicateDocument() =>
        new(ApplicationErrorCodes.Duplicate, "A patient with that document already exists.");
}
