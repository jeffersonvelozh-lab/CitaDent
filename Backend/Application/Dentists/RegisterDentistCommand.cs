namespace Application.Dentists;

/// <summary>A new dentist and the user account they sign in with.</summary>
public sealed record RegisterDentistCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string LicenseNumber,
    string? Specialty = null);
