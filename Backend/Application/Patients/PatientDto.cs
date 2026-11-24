using Domain.Patients;

namespace Application.Patients;

/// <summary>
/// A patient as the use cases return it. Age and "is minor" are left out on purpose:
/// they depend on the day of the query, so the caller works them out from BirthDate.
/// </summary>
public sealed record PatientDto(
    Guid Id,
    DocumentType DocumentType,
    string DocumentNumber,
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    string? Phone,
    string? Email,
    string? Address,
    string? GuardianName,
    string? GuardianPhone,
    bool IsActive)
{
    public static PatientDto From(Patient patient) => new(
        patient.Id,
        patient.DocumentType,
        patient.DocumentNumber,
        patient.FirstName,
        patient.LastName,
        patient.BirthDate,
        patient.Phone,
        patient.Email,
        patient.Address,
        patient.GuardianName,
        patient.GuardianPhone,
        patient.IsActive);
}
