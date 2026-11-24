using System.ComponentModel.DataAnnotations;
using Domain.Patients;

namespace WebApiRest.Contracts.Patients;

/// <summary>
/// Body of POST /api/patients. DocumentType and BirthDate are required here because a missing value type
/// would otherwise arrive as its default (for example 0001-01-01) and look valid.
/// </summary>
public sealed record CreatePatientRequest(
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
