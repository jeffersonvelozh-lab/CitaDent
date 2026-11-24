using Domain.Patients;

namespace Application.Patients;

public sealed record CreatePatientCommand(
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
