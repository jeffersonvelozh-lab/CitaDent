namespace Application.Dentists;

/// <summary>The dentist's new personal data; it replaces the old one. A blank specialty clears it.</summary>
public sealed record UpdateDentistCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string LicenseNumber,
    string? Specialty = null);
