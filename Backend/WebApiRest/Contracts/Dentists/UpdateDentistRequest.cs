namespace WebApiRest.Contracts.Dentists;

/// <summary>
/// Body of PUT /api/dentists/{id}: the dentist's complete personal data, which replaces the old one.
/// A missing or empty specialty clears it. The email and password are not edited here.
/// </summary>
public sealed record UpdateDentistRequest(
    string? FirstName,
    string? LastName,
    string? LicenseNumber,
    string? Specialty = null);
