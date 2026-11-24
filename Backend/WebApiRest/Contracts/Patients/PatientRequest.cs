using System.ComponentModel.DataAnnotations;
using Domain.Patients;

namespace WebApiRest.Contracts.Patients;

/// <summary>
/// Body of POST /api/patients and PUT /api/patients/{id}. DocumentType and BirthDate are required here
/// because a missing value type would otherwise arrive as its default (for example 0001-01-01) and look valid.
/// On PUT the body is the full new data: optional fields left out are cleared.
/// </summary>
public sealed record PatientRequest(
    [Required] DocumentType? DocumentType,
    string? DocumentNumber,
    string? FirstName,
    string? LastName,
    [Required] DateOnly? BirthDate,
    string? Phone = null,
    string? Email = null,
    string? Address = null,
    string? GuardianName = null,
    string? GuardianPhone = null);
