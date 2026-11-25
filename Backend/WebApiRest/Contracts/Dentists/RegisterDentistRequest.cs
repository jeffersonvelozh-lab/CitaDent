namespace WebApiRest.Contracts.Dentists;

/// <summary>
/// Body of POST /api/dentists: the dentist and the account they will sign in with.
/// The password is only used to compute its hash; it is never returned.
/// </summary>
public sealed record RegisterDentistRequest(
    string? Email,
    string? Password,
    string? FirstName,
    string? LastName,
    string? LicenseNumber,
    string? Specialty = null);
