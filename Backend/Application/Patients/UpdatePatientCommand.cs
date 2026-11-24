using Domain.Patients;

namespace Application.Patients;

/// <summary>The full new data of a patient: optional fields left out are cleared.</summary>
public sealed record UpdatePatientCommand(
    Guid Id,
    DocumentType DocumentType,
    string DocumentNumber,
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    string? Phone = null,
    string? Email = null,
    string? Address = null,
    string? GuardianName = null,
    string? GuardianPhone = null);
