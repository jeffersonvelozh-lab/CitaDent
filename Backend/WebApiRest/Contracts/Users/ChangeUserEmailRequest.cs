namespace WebApiRest.Contracts.Users;

/// <summary>Body of PUT /api/users/{id}/email.</summary>
public sealed record ChangeUserEmailRequest(string? Email);
