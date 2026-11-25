namespace WebApiRest.Contracts.Users;

/// <summary>Body of PUT /api/users/{id}/full-name.</summary>
public sealed record ChangeUserFullNameRequest(string? FullName);
